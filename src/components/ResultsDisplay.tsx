import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { CheckCircle, Download, ExternalLink, FileText, Copy } from 'lucide-react';
import { AutomationResult } from '@/types/automation';
import { useToast } from '@/hooks/use-toast';
import { useEffect, useState } from 'react';
import { getResults } from '@/Services/ResultService';

interface ResultsDisplayProps {
  result: AutomationResult;
  onDownload: () => void;
}

// Helper to extract market and link from a line
function extractMarketAndLink(line: string) {
  // Example: [Belgium] (V99) https://... or [Belgium] (V99) http://...
  const match = line.match(/^\s*\[([^\]]+)\]\s*\(([^)]+)\)\s*(https?:\/\/[^\s)\]]+)/);
  if (match) {
    return {
      market: match[1],
      variant: match[2],
      url: match[3]
    };
  }
  // fallback: just extract url
  const urlMatch = line.match(/(https?:\/\/[^\s)\]]+)/);
  return urlMatch ? { market: '', variant: '', url: urlMatch[1] } : null;
}

export const ResultsDisplay = ({ result, onDownload }: ResultsDisplayProps) => {
  const { toast } = useToast();
  const [fileContent, setFileContent] = useState<string | null>(null);
  const [isTruncated, setIsTruncated] = useState(false);
  const [parsedLinks, setParsedLinks] = useState<{market: string, variant: string, url: string}[]>([]);

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    toast({
      title: "Copied to clipboard",
      description: "Link copied successfully",
    });
  };

  const copyAllLinks = () => {
    const allLinks = result.generatedLinks.length > 0 ? result.generatedLinks.join('\n') : parsedLinks.map(link => link.url).join('\n');
    navigator.clipboard.writeText(allLinks);
    toast({
      title: "All links copied",
      description: `${result.generatedLinks.length > 0 ? result.generatedLinks.length : parsedLinks.length} links copied to clipboard`,
    });
  };

  useEffect(() => {
    if (result && result.fileLocation) {
      const match = result.fileLocation.match(/CRQ[_-]?(\w+)[_-](live|pgl|batch)\.txt/i);
      if (match) {
        const crqNumber = match[1];
        const mode = match[2];
        getResults(crqNumber, mode)
          .then((content) => {
            if (!content) {
              setFileContent('Could not fetch file content.');
              setIsTruncated(false);
              setParsedLinks([]);
              return;
            }
            // Remove all truncation logic
            setFileContent(content);
            setIsTruncated(false);
            // Extract links with market info
            const lines = content.split(/\r?\n/);
            const foundLinks = lines
              .map(line => extractMarketAndLink(line))
              .filter(Boolean) as {market: string, variant: string, url: string}[];
            setParsedLinks(foundLinks);
          })
          .catch(() => {
            setFileContent('Could not fetch file content.');
            setIsTruncated(false);
            setParsedLinks([]);
          });
      }
    }
  }, [result]);

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
          {/* Always show file content from result API, even if empty or error */}
          <div className="mt-4 p-4 border rounded bg-muted/30">
            <div className="font-semibold mb-2">File Content (from /api/Crq/results):</div>
            {fileContent === null ? (
              <span className="text-xs text-muted-foreground">Loading...</span>
            ) : (
              <>
                {isTruncated && (
                  <div className="mb-2 text-warning text-xs font-semibold flex items-center gap-2">
                    <span>File is too large to display fully. Showing partial content.</span>
                    <Button variant="link" size="sm" onClick={onDownload} className="p-0 h-auto min-w-0">
                      <Download className="w-3 h-3 mr-1" /> Download full file
                    </Button>
                  </div>
                )}
                <pre className="whitespace-pre-wrap text-sm max-h-96 overflow-auto">{fileContent}</pre>
              </>
            )}
          </div>
        </div>
        {/* Generated Links */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="font-medium">Generated Links</h3>
            <div className="flex items-center space-x-2">
              <Badge variant="secondary">
                {(result.generatedLinks.length > 0 ? result.generatedLinks.length : parsedLinks.length)} links
              </Badge>
              <Button variant="outline" size="sm" onClick={copyAllLinks} disabled={result.generatedLinks.length === 0 && parsedLinks.length === 0}>
                <Copy className="w-4 h-4 mr-2" />
                Copy All
              </Button>
            </div>
          </div>
          <div className="space-y-2 max-h-64 overflow-auto border rounded-lg p-3 bg-muted/30">
            {(result.generatedLinks.length > 0 ? result.generatedLinks : parsedLinks).length > 0 ?
              (result.generatedLinks.length > 0 ? result.generatedLinks : parsedLinks).map((linkObj, index) => {
                // If using parsedLinks, linkObj is {market, variant, url}, else it's a string
                if (typeof linkObj === 'string') {
                  // fallback for old generatedLinks
                  return (
                    <div key={index} className="flex items-center justify-between p-2 bg-background rounded border group hover:bg-muted/50 transition-colors">
                      <span className="text-sm font-mono text-muted-foreground flex-1 mr-2">{linkObj}</span>
                      <div className="flex items-center space-x-1 opacity-0 group-hover:opacity-100 transition-opacity">
                        <Button variant="ghost" size="sm" onClick={() => copyToClipboard(linkObj)} className="h-8 w-8 p-0"><Copy className="w-3 h-3" /></Button>
                        <Button variant="ghost" size="sm" onClick={() => window.open(linkObj, '_blank')} className="h-8 w-8 p-0"><ExternalLink className="w-3 h-3" /></Button>
                      </div>
                    </div>
                  );
                } else {
                  // parsedLinks with market info
                  return (
                    <div key={index} className="flex items-center justify-between p-2 bg-background rounded border group hover:bg-muted/50 transition-colors">
                      <span className="text-sm font-mono text-muted-foreground flex-1 mr-2">
                        {linkObj.market && <span className="font-semibold">[{linkObj.market}]</span>} {linkObj.variant && <span>({linkObj.variant})</span>} {linkObj.url}
                      </span>
                      <div className="flex items-center space-x-1 opacity-0 group-hover:opacity-100 transition-opacity">
                        <Button variant="ghost" size="sm" onClick={() => copyToClipboard(linkObj.url)} className="h-8 w-8 p-0"><Copy className="w-3 h-3" /></Button>
                        <Button variant="ghost" size="sm" onClick={() => window.open(linkObj.url, '_blank')} className="h-8 w-8 p-0"><ExternalLink className="w-3 h-3" /></Button>
                      </div>
                    </div>
                  );
                }
              })
              : (
                <div className="text-xs text-muted-foreground">No links found in file content.</div>
              )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
};