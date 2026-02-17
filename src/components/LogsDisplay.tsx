// ...existing code...
import { useEffect, useRef, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
// Badge component not imported because a simple inline element is used for the "Live" indicator.
import { Terminal, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { LogEntry } from '@/types/automation';

export const LogsDisplay = ({ isRunning, onClearLogs }: { isRunning: boolean; onClearLogs: () => void }) => {

  useEffect(() => {
    if (isRunning) startStreaming();
    else stopStreaming();

    // cleanup on unmount
    return () => {
      stopStreaming();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isRunning]);

  const [logs, setLogs] = useState<LogEntry[]>([]);
  const esRef = useRef<EventSource | null>(null);

  const startStreaming = () => {
    if (esRef.current) return;
    const es = new EventSource('https://localhost:7053/api/Crq/getSSE'); // endpoint as requested
    esRef.current = es;

  es.onmessage = (ev) => {
    const raw = ev.data;
    // Try JSON, otherwise treat as plain text line
    let entry: LogEntry | any;
    try {
      entry = JSON.parse(raw) as LogEntry;
    }
    catch {
      // Fallback entry shape - adapt to your LogEntry type
      entry = {
        text: raw,
        timestamp: new Date().toISOString(),
      } as unknown as LogEntry;
    }
    setLogs((prev) => [...prev, entry]);
  };

    es.onerror = () => {
      // Close the connection on error; server or proxy may be buffering or closed.
      if (esRef.current) {
        try { esRef.current.close(); } catch {}
        esRef.current = null;
      }
    };
  };

  const stopStreaming = () => {
    if (!esRef.current) return;
    try { esRef.current.close(); } catch {}
    esRef.current = null;
  };

  const clearLogs = () => {
    setLogs([]);
    onClearLogs?.();
  };

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
      <CardHeader className="flex flex-row items-center justify-between">
        <div className="flex items-center gap-2 col">
          <CardTitle className="mx-auto">Execution Logs</CardTitle>
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
        <Button className=' bg-gray-200 w-10 h-10 text-red-500 hover:bg-red-500 ' variant="ghost" onClick={clearLogs} aria-label="Clear Logs">
          <Trash2 className="w-10 h-10" />
        </Button>
      </CardHeader>

      <CardContent>
        <div style={{ maxHeight: 300, overflow: 'auto', fontFamily: 'monospace', fontSize: 13 }}>
          {logs.length === 0 ? (
            <div style={{ color: '#6b7280' }}>No logs yet.</div>
          ) : (
            logs.map((log, idx) => (
              <div key={idx} style={{ padding: '6px 0', borderBottom: '1px solid #e5e7eb' }}>
                {/* <pre style={{ margin: 0 }}>
                  {JSON.stringify(log, null, 2)}
                </pre> */}
                {/* [{log.timestamp}] {log as any} */}
                [{log.timestamp}][{log.level}] - {log.message}
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
};