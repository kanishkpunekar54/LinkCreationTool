import { useState, useEffect } from 'react';
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
import { login } from '@/Services/LoginServices';
import { runGtp } from '@/Services/CrqServices';

const Index = () => {
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [credentials, setCredentials] = useState<LoginCredentials | null>(null);
  const [automationStatus, setAutomationStatus] = useState<AutomationStatus>('idle');
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [result, setResult] = useState<AutomationResult | null>(null);
  const [loginError, setLoginError] = useState<string | null>(null);
  const [fileContent, setFileContent] = useState<string | null>(null);
  const [showResults, setShowResults] = useState(false);

  // Helper to limit file content for display
  const getLimitedFileContent = (content: string) => {
    const MAX_LINES = 1000;
    const MAX_CHARS = 100 * 1024; // 100 KB
    let lines = content.split('\n');
    let limited = lines.slice(0, MAX_LINES).join('\n');
    if (limited.length > MAX_CHARS) limited = limited.slice(0, MAX_CHARS);
    const isTruncated = lines.length > MAX_LINES || content.length > MAX_CHARS;
    return { limited, isTruncated };
  };

  // Helper to ensure CRQ prefix
  const ensureCrqPrefix = (crq: string) => crq.startsWith('CRQ') ? crq : `CRQ${crq}`;

  const handleLogin = async (loginCredentials: LoginCredentials) => {
    setLoginError(null);
    try {
      await login(loginCredentials.username, loginCredentials.password);
      setCredentials(loginCredentials);
      setIsLoggedIn(true);
    } catch (error) {
      setLoginError((error as Error).message || 'Login failed');
    }
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

  const handleRunAutomation = async (config: AutomationConfig) => {
    setAutomationStatus('running');
    setResult(null);
    addLog('info', 'Starting automation process...');

    try {
      addLog('info', `Calling backend for CRQ: ${config.crqNumber}`);
      const response = await runGtp(
        config.crqNumber,
        config.mode,
        config.variants,
        config.gtpUrl
      );
      await new Promise(resolve => setTimeout(resolve, 2000));
      console.log('runGtp response:', response);
      addLog('success', typeof response === 'string' ? response : JSON.stringify(response));
      setResult({
        fileLocation: `${config.crqNumber}_${config.mode.toLowerCase() === 'batch' ? 'Batch' : config.mode.toLowerCase()}.txt`,
        generatedLinks: [],
        success: true
      });
      setAutomationStatus('completed');
    } catch (error) {
      setAutomationStatus('error');
      addLog('error', error && typeof error === 'object' && 'message' in error ? (error as Error).message : JSON.stringify(error));
      console.error('Automation error:', error);
    }
  };

  const fetchFileContent = async (crqNumber: string, mode: string) => {
    try {
      const crqWithPrefix = ensureCrqPrefix(crqNumber);
      const response = await fetch(`http://localhost:5116/api/Crq/results?crqNumber=${encodeURIComponent(crqWithPrefix)}&mode=${encodeURIComponent(mode)}`);
      if (!response.ok) throw new Error('Failed to fetch file content');
      const data = await response.json();
      setFileContent(data.content || 'No content found.');
    } catch (err) {
      setFileContent('Could not fetch file content.');
    }
  };

  const handleDownloadFile = async (crqNumber: string, mode: string) => {
    try {
      const crqWithPrefix = ensureCrqPrefix(crqNumber);
      const response = await fetch(`http://localhost:5116/api/Crq/download?crqNumber=${encodeURIComponent(crqWithPrefix)}&mode=${encodeURIComponent(mode)}`);
      if (!response.ok) throw new Error('File not found');
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${crqWithPrefix}_${mode}.txt`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    } catch (err) {
      addLog('error', 'Download failed: ' + (err as Error).message);
    }
  };

  useEffect(() => {
  if (automationStatus === 'completed' && result && result.success) {
    if (result.fileLocation) {
      const parts = result.fileLocation.split('_');
      const crqNumber = parts[0]; // ✅ Always the CRQ with prefix
      const mode = parts[1]?.replace(".txt", "").toLowerCase(); // ✅ Always the mode
      if (crqNumber && mode) fetchFileContent(crqNumber, mode);
    }
  }
  // eslint-disable-next-line
}, [automationStatus, result]);

  const handleClearLogs = () => {
    setLogs([]);
  };

  const handleDownload = () => {
    if (result) {
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
    return <LoginForm onLogin={handleLogin} loginError={loginError} />;
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
          {result && automationStatus === 'completed' && (
          <ResultsDisplay 
              result={result} 
            onDownload={() => {
              const fileLocation = result.fileLocation || "";
              const parts = fileLocation.split("_");

              let crqNumber = parts[0]; // ✅ Keep prefix intact
              let mode = parts[1]?.replace(".txt", "");

              if (crqNumber && mode) {
                handleDownloadFile(crqNumber, mode);
              }
            }} 
          />

          )}
        </div>
      </div>
    </div>
  );
};

export default Index;
