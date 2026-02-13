// ...existing code...
import { useEffect, useRef, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
// Badge component not imported because a simple inline element is used for the "Live" indicator.
import { Terminal, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { LogEntry } from '@/types/automation';

export const LogsDisplay = ({ isRunning, onClearLogs }: { isRunning: boolean; onClearLogs: () => void }) => {

  useEffect(() => {
    if (isRunning) {
      startStreaming();
    }
       
  }, [ isRunning]);

  const [logs, setLogs] = useState<LogEntry[]>([]);
  const esRef = useRef<EventSource | null>(null);

  const startStreaming = () => {
    if (esRef.current) return;
    const es = new EventSource('https://localhost:7053/api/Crq/getSSE'); // endpoint as requested
    esRef.current = es;

    es.onmessage = (ev) => {
      try {
        const parsed = JSON.parse(ev.data) as LogEntry;
        setLogs((prev) => [...prev, parsed]);
      } catch {
        // ignore malformed chunks
      }
    };

    es.onerror = () => {
      // close on error and mark stopped
      es.close();
      esRef.current = null;
    };
  };

  const stopStreaming = () => {
    if (!esRef.current) return;
    esRef.current.close();
    esRef.current = null;
  };

  const clearLogs = () => setLogs([]);

  useEffect(() => {
    return () => {
      if (esRef.current) {
        esRef.current.close();
        esRef.current = null;
      }
    };
  }, []);

  return (
    <Card>
      <CardHeader className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Terminal />
          <CardTitle>Logs</CardTitle>
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
              marginLeft: 12,
              fontSize: 12,
            }}
          >
            <span
              style={{
                width: 8,
                height: 8,
                borderRadius: 8,
                background: isRunning ? '#10b981' : '#9ca3af',
                display: 'inline-block',
              }}
            />
            {isRunning ? 'Live' : 'Offline'}
          </span>
        </div>

        <div className="flex items-center gap-2">
          <Button variant="ghost" onClick={clearLogs} aria-label="Clear logs">
            <Trash2 />
          </Button>
        </div>
      </CardHeader>

      <CardContent>
        <div style={{ maxHeight: 300, overflow: 'auto', fontFamily: 'monospace', fontSize: 13 }}>
          {logs.length === 0 ? (
            <div style={{ color: '#6b7280' }}>No logs yet.</div>
          ) : (
            logs.map((log, idx) => (
              <div key={idx} style={{ padding: '6px 0', borderBottom: '1px solid #e5e7eb' }}>
                <pre style={{ margin: 0 }}>{JSON.stringify(log, null, 2)}</pre>
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
};