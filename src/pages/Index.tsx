import { useState } from 'react';
import { FileText } from 'lucide-react';
import { LoginForm } from '@/components/LoginForm';
import { AutomationDashboard } from '@/components/AutomationDashboard';
import { LogsDisplay } from '@/components/LogsDisplay';
import { ResultsDisplay } from '@/components/ResultsDisplay';
import { 
  LoginCredentials, 
  AutomationConfig, 
  LogEntry, 
  AutomationResult, 
  AutomationStatus 
} from '@/types/automation';

const Index = () => {
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [credentials, setCredentials] = useState<LoginCredentials | null>(null);
  const [automationStatus, setAutomationStatus] = useState<AutomationStatus>('idle');
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [result, setResult] = useState<AutomationResult | null>(null);

  const handleLogin = (loginCredentials: LoginCredentials) => {
    setCredentials(loginCredentials);
    setIsLoggedIn(true);
  };

  const handleLogout = () => {
    setIsLoggedIn(false);
    setCredentials(null);
    setAutomationStatus('idle');
    setLogs([]);
    setResult(null);
  };

  const addLog = (level: LogEntry['level'], message: string) => {
    const newLog: LogEntry = {
      id: Date.now().toString(),
      timestamp: new Date().toLocaleTimeString(),
      level,
      message
    };
    setLogs(prev => [...prev, newLog]);
  };

  const simulateAutomation = async (config: AutomationConfig) => {
    // Simulate automation process with mock logs
    const steps = [
      { level: 'info' as const, message: 'Initializing automation...' },
      { level: 'info' as const, message: `Connecting to GTP URL: ${config.gtpUrl}` },
      { level: 'info' as const, message: `Authenticating user: ${credentials?.username}` },
      { level: 'success' as const, message: 'Authentication successful' },
      { level: 'info' as const, message: `Processing CRQ: ${config.crqNumber}` },
      { level: 'info' as const, message: `Selected variants: ${config.variants.join(', ')}` },
      { level: 'info' as const, message: `Running in ${config.mode} mode` },
      { level: 'info' as const, message: 'Launching browser automation...' },
      { level: 'info' as const, message: 'Navigating to target pages...' },
      { level: 'info' as const, message: 'Extracting required data...' },
      { level: 'info' as const, message: 'Generating output links...' },
      { level: 'success' as const, message: 'Automation completed successfully!' },
    ];

    for (let i = 0; i < steps.length; i++) {
      await new Promise(resolve => setTimeout(resolve, 800 + Math.random() * 400));
      addLog(steps[i].level, steps[i].message);
    }

    // Generate mock results
    const mockResult: AutomationResult = {
      fileLocation: 'C:\\Users\\Username\\Documents\\CRQ_Results\\CRQ_' + config.crqNumber + '_output.txt',
      generatedLinks: config.variants.map(variant => 
        `https://gtp.example.com/results/${config.crqNumber}/${variant.toLowerCase()}/${config.mode.toLowerCase()}`
      ),
      success: true
    };

    setResult(mockResult);
    setAutomationStatus('completed');
  };

  const handleRunAutomation = async (config: AutomationConfig) => {
    setAutomationStatus('running');
    setResult(null);
    addLog('info', 'Starting automation process...');
    
    try {
      await simulateAutomation(config);
    } catch (error) {
      setAutomationStatus('error');
      addLog('error', 'Automation failed: ' + (error as Error).message);
    }
  };

  const handleClearLogs = () => {
    setLogs([]);
  };

  const handleDownload = () => {
    if (result) {
      // Create a mock file download
      const content = result.generatedLinks.join('\n');
      const blob = new Blob([content], { type: 'text/plain' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `CRQ_${Date.now()}_output.txt`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }
  };

  if (!isLoggedIn) {
    return <LoginForm onLogin={handleLogin} />;
  }

  return (
    <div className="min-h-screen bg-background">
      <AutomationDashboard
        credentials={credentials!}
        onLogout={handleLogout}
        onRunAutomation={handleRunAutomation}
        automationStatus={automationStatus}
      />
      
      <div className="container mx-auto px-4 pb-8">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          {/* Logs Section */}
          <LogsDisplay
            logs={logs}
            isRunning={automationStatus === 'running'}
            onClearLogs={handleClearLogs}
          />

          {/* Results Section */}
          <div>
            {result && automationStatus === 'completed' ? (
              <ResultsDisplay result={result} onDownload={handleDownload} />
            ) : (
              <div className="h-full flex items-center justify-center text-muted-foreground border-2 border-dashed rounded-lg">
                <div className="text-center p-8">
                  <FileText className="w-12 h-12 mx-auto mb-4 opacity-50" />
                  <p>Results will appear here after automation completes</p>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default Index;
