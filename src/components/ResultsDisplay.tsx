import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { CheckCircle, Download, ExternalLink, FileText, Copy } from 'lucide-react';
import { AutomationResult } from '@/types/automation';
import { useToast } from '@/hooks/use-toast';

interface ResultsDisplayProps {
  result: AutomationResult;
  onDownload: () => void;
}

export const ResultsDisplay = ({ result, onDownload }: ResultsDisplayProps) => {
  const { toast } = useToast();

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    toast({
      title: "Copied to clipboard",
      description: "Link copied successfully",
    });
  };

  const copyAllLinks = () => {
    const allLinks = result.generatedLinks.join('\n');
    navigator.clipboard.writeText(allLinks);
    toast({
      title: "All links copied",
      description: `${result.generatedLinks.length} links copied to clipboard`,
    });
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center space-x-2">
          <CheckCircle className="w-5 h-5 text-success" />
          <CardTitle className="text-success">Automation Completed</CardTitle>
        </div>
      </CardHeader>
      <CardContent className="space-y-6">
        {/* File Location */}
        <div className="p-4 border rounded-lg bg-muted/50">
          <div className="flex items-center justify-between mb-2">
            <div className="flex items-center space-x-2">
              <FileText className="w-4 h-4 text-muted-foreground" />
              <span className="font-medium">Output File</span>
            </div>
            <Button variant="outline" size="sm" onClick={onDownload}>
              <Download className="w-4 h-4 mr-2" />
              Download
            </Button>
          </div>
          <p className="text-sm text-muted-foreground font-mono bg-background p-2 rounded border">
            {result.fileLocation}
          </p>
        </div>

        {/* Generated Links */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="font-medium">Generated Links</h3>
            <div className="flex items-center space-x-2">
              <Badge variant="secondary">
                {result.generatedLinks.length} links
              </Badge>
              <Button variant="outline" size="sm" onClick={copyAllLinks}>
                <Copy className="w-4 h-4 mr-2" />
                Copy All
              </Button>
            </div>
          </div>
          
          <div className="space-y-2 max-h-64 overflow-auto border rounded-lg p-3 bg-muted/30">
            {result.generatedLinks.map((link, index) => (
              <div 
                key={index} 
                className="flex items-center justify-between p-2 bg-background rounded border group hover:bg-muted/50 transition-colors"
              >
                <span className="text-sm font-mono text-muted-foreground flex-1 mr-2">
                  {link}
                </span>
                <div className="flex items-center space-x-1 opacity-0 group-hover:opacity-100 transition-opacity">
                  <Button 
                    variant="ghost" 
                    size="sm"
                    onClick={() => copyToClipboard(link)}
                    className="h-8 w-8 p-0"
                  >
                    <Copy className="w-3 h-3" />
                  </Button>
                  <Button 
                    variant="ghost" 
                    size="sm"
                    onClick={() => window.open(link, '_blank')}
                    className="h-8 w-8 p-0"
                  >
                    <ExternalLink className="w-3 h-3" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </div>
      </CardContent>
    </Card>
  );
};