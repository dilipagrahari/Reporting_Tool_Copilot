import { Component, Input, OnInit } from '@angular/core';
import { NgClass, NgIf, DatePipe } from '@angular/common';
import { ChatMessage } from '../copilot.models';
import { MarkdownRendererComponent } from '../markdown-renderer/markdown-renderer';

@Component({
  selector: 'app-chat-message',
  standalone: true,
  imports: [NgClass, NgIf, DatePipe, MarkdownRendererComponent],
  templateUrl: './chat-message.html',
  styleUrl: './chat-message.css',
})
export class ChatMessageComponent implements OnInit {
  @Input({ required: true }) message!: ChatMessage;

  copySuccess = false;

  ngOnInit(): void {}

  get isUser(): boolean {
    return this.message.role === 'user';
  }

  onCopy(): void {
    navigator.clipboard.writeText(this.message.content).then(() => {
      this.copySuccess = true;
      setTimeout(() => (this.copySuccess = false), 2000);
    });
  }

  trackByChar(_: number, c: string): string {
    return c;
  }
}
