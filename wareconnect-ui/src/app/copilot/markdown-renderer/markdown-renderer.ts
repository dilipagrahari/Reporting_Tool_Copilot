import { Component, Input, OnChanges } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
  selector: 'app-markdown-renderer',
  standalone: true,
  template: '<div class="md-body" [innerHTML]="html"></div>',
  styleUrl: './markdown-renderer.css',
})
export class MarkdownRendererComponent implements OnChanges {
  @Input() content = '';
  html: SafeHtml = '';

  constructor(private readonly sanitizer: DomSanitizer) {}

  ngOnChanges(): void {
    this.html = this.sanitizer.bypassSecurityTrustHtml(this.parse(this.content));
  }

  private parse(md: string): string {
    let html = md;

    // Code blocks (must come before inline code)
    html = html.replace(/```(\w*)\n?([\s\S]*?)```/g, (_, lang, code) => {
      const escaped = code.replace(/</g, '&lt;').replace(/>/g, '&gt;');
      return `<pre class="md-pre"><code class="md-code${lang ? ' lang-' + lang : ''}">${escaped.trim()}</code></pre>`;
    });

    // Tables
    html = html.replace(
      /^\|(.+)\|\n\|[-| :]+\|\n((?:\|.+\|\n?)*)/gm,
      (_, header, rows) => {
        const ths = header
          .split('|')
          .filter((c: string) => c.trim())
          .map((c: string) => `<th>${c.trim()}</th>`)
          .join('');
        const trs = rows
          .trim()
          .split('\n')
          .map((row: string) => {
            const tds = row
              .split('|')
              .filter((c: string) => c.trim())
              .map((c: string) => `<td>${c.trim()}</td>`)
              .join('');
            return `<tr>${tds}</tr>`;
          })
          .join('');
        return `<table class="md-table"><thead><tr>${ths}</tr></thead><tbody>${trs}</tbody></table>`;
      }
    );

    // Headings
    html = html.replace(/^### (.+)$/gm, '<h3 class="md-h3">$1</h3>');
    html = html.replace(/^## (.+)$/gm, '<h2 class="md-h2">$1</h2>');
    html = html.replace(/^# (.+)$/gm, '<h1 class="md-h1">$1</h1>');

    // Blockquote
    html = html.replace(/^> (.+)$/gm, '<blockquote class="md-blockquote">$1</blockquote>');

    // Bold + italic
    html = html.replace(/\*\*\*(.+?)\*\*\*/g, '<strong><em>$1</em></strong>');
    html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/\*(.+?)\*/g, '<em>$1</em>');

    // Inline code
    html = html.replace(/`([^`]+)`/g, '<code class="md-inline-code">$1</code>');

    // Links
    html = html.replace(/\[(.+?)\]\((.+?)\)/g, '<a class="md-link" href="$2" target="_blank" rel="noopener">$1</a>');

    // Unordered lists
    html = html.replace(/^([\-\*] .+(\n|$))+/gm, (block) => {
      const items = block
        .trim()
        .split('\n')
        .map((line) => `<li>${line.replace(/^[\-\*] /, '')}</li>`)
        .join('');
      return `<ul class="md-ul">${items}</ul>`;
    });

    // Ordered lists
    html = html.replace(/^(\d+\. .+(\n|$))+/gm, (block) => {
      const items = block
        .trim()
        .split('\n')
        .map((line) => `<li>${line.replace(/^\d+\. /, '')}</li>`)
        .join('');
      return `<ol class="md-ol">${items}</ol>`;
    });

    // Paragraphs (lines not already wrapped)
    html = html
      .split('\n\n')
      .map((para) => {
        const trimmed = para.trim();
        if (!trimmed) return '';
        if (/^<(h[1-3]|ul|ol|pre|table|blockquote)/.test(trimmed)) return trimmed;
        return `<p class="md-p">${trimmed.replace(/\n/g, '<br>')}</p>`;
      })
      .join('');

    return html;
  }
}
