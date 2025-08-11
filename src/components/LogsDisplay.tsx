import { useEffect, useRef } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Terminal, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { LogEntry } from '@/types/automation';

interface LogsDisplayProps {
  logs: LogEntry[];
  isRunning: boolean;
  onClearLogs: () => void;
}

export const LogsDisplay = ({ logs, isRunning, onClearLogs }: LogsDisplayProps) => {
  const logsEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    logsEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [logs]);

  const getLevelColor = (level: LogEntry['level']) => {
    switch (level) {
      case 'error': return 'text-red-400';
      case 'warning': return 'text-yellow-400';
      case 'success': return 'text-green-400';
      default: return 'text-terminal-foreground';
    }
  };

  const getLevelIcon = (level: LogEntry['level']) => {
    switch (level) {
      case 'error': return '✕';
      case 'warning': return '⚠';
      case 'success': return '✓';
      default: return '→';
    }
  };

  return (
    <Card className="h-full">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-2">
            <Terminal className="w-5 h-5" />
            <CardTitle>Automation Logs</CardTitle>
            {isRunning && (
              <Badge variant="outline" className="bg-success/10 text-success border-success/20">
                Live
              </Badge>
            )}
          </div>
          <Button 
            variant="outline" 
            size="sm" 
            onClick={onClearLogs}
            disabled={logs.length === 0}
          >
            <Trash2 className="w-4 h-4 mr-2" />
            Clear
          </Button>
        </div>
      </CardHeader>
      <CardContent className="p-0">
        <div className="bg-terminal rounded-lg mx-6 mb-6 h-96 overflow-auto border">
          <div className="p-4 font-mono text-sm">
            {logs.length === 0 ? (
              <div className="flex items-center justify-center h-full text-terminal-muted">
                <div className="text-center">
                  <Terminal className="w-8 h-8 mx-auto mb-2 opacity-50" />
                  <p>No logs yet. Run automation to see output.</p>
                </div>
              </div>
            ) : (
              logs.map((log) => (
                <div key={log.id} className="mb-2 flex items-start space-x-3">
                  <span className="text-terminal-muted text-xs mt-0.5 w-20 flex-shrink-0">
                    {log.timestamp}
                  </span>
                  <span className={`w-4 flex-shrink-0 ${getLevelColor(log.level)}`}>
                    {getLevelIcon(log.level)}
                  </span>
                  <span className={`${getLevelColor(log.level)} break-words`}>
                    {log.message}
                  </span>
                </div>
              ))
            )}
            {isRunning && (
              <div className="flex items-center space-x-2 text-terminal-foreground">
                <div className="w-2 h-2 bg-success rounded-full animate-pulse"></div>
                <span className="text-terminal-muted">Running...</span>
              </div>
            )}
            <div ref={logsEndRef} />
          </div>
        </div>
      </CardContent>
    </Card>
  );
};