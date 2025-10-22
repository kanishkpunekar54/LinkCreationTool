import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';

import { Play, User, LogOut, Settings, Search } from 'lucide-react';
import { AutomationConfig, LoginCredentials, AutomationStatus } from '@/types/automation';

interface AutomationDashboardProps {
  credentials: LoginCredentials;
  onLogout: () => void;
  onRunAutomation: (config: AutomationConfig) => void;
  automationStatus: AutomationStatus;
}

const VARIANTS = [
  'V85', 'V86', 'V87', 'V88', 'V89', 'V90', 
  'V92', 'V93', 'V94', 'V95', 'V96', 'V97', 'V98', 'V99'
];

const MODES = ['PGL', 'Live', 'Batch'] as const;

export const AutomationDashboard = ({ 
  credentials, 
  onLogout, 
  onRunAutomation, 
  automationStatus 
}: AutomationDashboardProps) => {
  const [config, setConfig] = useState<AutomationConfig>({
    crqNumber: '',
    variants: [],
    gtpUrl: '',
    mode: 'PGL'
  });

  const [variantSearch, setVariantSearch] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (config.crqNumber && config.variants.length > 0 && config.gtpUrl) {
      onRunAutomation(config);
    }
  };

  const handleVariantChange = (variant: string, checked: boolean) => {
    setConfig(prev => ({
      ...prev,
      variants: checked 
        ? [...prev.variants, variant]
        : prev.variants.filter(v => v !== variant)
    }));
  };

  const handleModeChange = (mode: string) => {
    setConfig(prev => ({ ...prev, mode: mode as typeof MODES[number] }));
  };

  const filteredVariants = VARIANTS.filter(variant => 
    variant.toLowerCase().includes(variantSearch.toLowerCase())
  );

  const isRunning = automationStatus === 'running';

  return (
    <div className="min-h-screen bg-background">
      {/* Header */}
      <header className="border-b bg-card">
        <div className="container mx-auto px-4 py-4 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 bg-primary rounded-lg flex items-center justify-center">
              <Settings className="w-4 h-4 text-primary-foreground" />
            </div>
            <div>
              <h1 className="text-xl font-bold">Link Pilot</h1>
              <p className="text-sm text-muted-foreground">Automation Dashboard</p>
            </div>
          </div>
          <div className="flex items-center space-x-4">
            <div className="flex items-center space-x-2">
              <User className="w-4 h-4 text-muted-foreground" />
              <span className="text-sm font-medium">{credentials.username}</span>
            </div>
            <Button variant="outline" size="sm" onClick={onLogout}>
              <LogOut className="w-4 h-4 mr-2" />
              Logout
            </Button>
          </div>
        </div>
      </header>

      <div className="container mx-auto px-4 py-8">
        {/* Configuration Panel - Full Width */}
          {/* Configuration Panel */}
          <Card>
            <CardHeader>
              <CardTitle>Automation Configuration</CardTitle>
              <CardDescription>
                Configure your automation parameters
              </CardDescription>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleSubmit} className="space-y-6">
                {/* CRQ Number */}
                <div className="space-y-2">
                  <Label htmlFor="crqNumber">CRQ Number</Label>
                  <Input
                    id="crqNumber"
                    type="text"
                    placeholder="Enter CRQ number"
                    value={config.crqNumber}
                    onChange={(e) => setConfig(prev => ({ ...prev, crqNumber: e.target.value }))}
                    disabled={isRunning}
                    required
                  />
                </div>

                {/* Variants */}
                <div className="space-y-3">
                  <Label>Variants Selection</Label>
                  
                  {/* Search Box */}
                  <div className="relative">
                    <Search className="absolute left-3 top-3 w-4 h-4 text-muted-foreground" />
                    <Input
                      placeholder="Search variants (e.g., V85, V99)..."
                      value={variantSearch}
                      onChange={(e) => setVariantSearch(e.target.value)}
                      className="pl-10"
                      disabled={isRunning}
                    />
                  </div>

                  {/* Variants Grid */}
                  <div className="border rounded-lg p-4 bg-muted/30 max-h-48 overflow-auto">
                    <div className="grid grid-cols-4 gap-3">
                       {filteredVariants.map((variant) => (
                         <div 
                           key={variant} 
                           className={`flex items-center space-x-2 p-2 rounded-md border transition-colors ${
                             config.variants.includes(variant) 
                               ? 'bg-primary/10 border-primary/30' 
                               : 'bg-background hover:border-primary/20'
                           }`}
                         >
                           <Checkbox
                             id={variant}
                             checked={config.variants.includes(variant)}
                             onCheckedChange={(checked) => 
                               handleVariantChange(variant, checked as boolean)
                             }
                             disabled={isRunning}
                           />
                           <Label 
                             htmlFor={variant} 
                             className="text-sm font-medium cursor-pointer flex-1"
                           >
                             {variant}
                           </Label>
                         </div>
                       ))}
                    </div>
                    
                    {filteredVariants.length === 0 && variantSearch && (
                      <div className="text-center py-4 text-muted-foreground">
                        <p className="text-sm">No variants found matching "{variantSearch}"</p>
                      </div>
                    )}
                  </div>

                  {/* Selected Variants Display */}
                  {config.variants.length > 0 && (
                    <div className="space-y-2">
                      <div className="flex items-center justify-between">
                        <Label className="text-sm font-medium">
                          Selected ({config.variants.length})
                        </Label>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setConfig(prev => ({ ...prev, variants: [] }))}
                          disabled={isRunning}
                        >
                          Clear All
                        </Button>
                      </div>
                      <div className="flex flex-wrap gap-2 p-3 border rounded-lg bg-background">
                        {config.variants.sort().map((variant) => (
                          <Badge 
                            key={variant} 
                            variant="secondary" 
                            className="text-xs cursor-pointer hover:bg-destructive hover:text-destructive-foreground transition-colors"
                            onClick={() => handleVariantChange(variant, false)}
                          >
                            {variant} ×
                          </Badge>
                        ))}
                      </div>
                    </div>
                  )}
                </div>

                {/* GTP URL */}
                <div className="space-y-2">
                  <Label htmlFor="gtpUrl">GTP URL</Label>
                  <Input
                    id="gtpUrl"
                    type="url"
                    placeholder="https://gtp.example.com/..."
                    value={config.gtpUrl}
                    onChange={(e) => setConfig(prev => ({ ...prev, gtpUrl: e.target.value }))}
                    disabled={isRunning}
                    required
                  />
                </div>

                {/* Mode Selection */}
                <div className="space-y-3">
                  <Label>Mode</Label>
                  <div className="flex bg-muted rounded-lg p-1">
                    {MODES.map((mode) => (
                      <Button
                        key={mode}
                        type="button"
                        variant={config.mode === mode ? "default" : "ghost"}
                        size="sm"
                        onClick={() => handleModeChange(mode)}
                        disabled={isRunning}
                        className={`flex-1 ${
                          config.mode === mode 
                            ? 'bg-background text-foreground shadow-sm' 
                            : 'hover:bg-background/50'
                        }`}
                      >
                        {mode}
                      </Button>
                    ))}
                  </div>
                </div>

                {/* Submit Button */}
                <Button 
                  type="submit" 
                  className="w-full"
                  disabled={
                    isRunning || 
                    !config.crqNumber || 
                    config.variants.length === 0 || 
                    !config.gtpUrl
                  }
                >
                  {isRunning ? (
                    <>
                      <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin mr-2" />
                      Running Automation...
                    </>
                  ) : (
                    <>
                      <Play className="w-4 h-4 mr-2" />
                      Run Automation
                    </>
                  )}
                </Button>
              </form>
            </CardContent>
        </Card>
      </div>
    </div>
  );
};