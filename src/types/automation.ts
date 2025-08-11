export interface LoginCredentials {
  username: string;
  password: string;
}

export interface AutomationConfig {
  crqNumber: string;
  variants: string[];
  gtpUrl: string;
  mode: 'PGL' | 'Live' | 'Batch';
}

export interface LogEntry {
  id: string;
  timestamp: string;
  level: 'info' | 'warning' | 'error' | 'success';
  message: string;
}

export interface AutomationResult {
  fileLocation: string;
  generatedLinks: string[];
  success: boolean;
}

export type AutomationStatus = 'idle' | 'running' | 'completed' | 'error';