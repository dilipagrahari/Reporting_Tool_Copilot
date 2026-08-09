import {
  Component,
  ElementRef,
  OnInit,
  ViewChild,
} from '@angular/core';
import { AsyncPipe, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { CopilotState } from '../copilot.models';
import { CopilotService } from '../copilot.service';

@Component({
  selector: 'app-chat-input',
  standalone: true,
  imports: [AsyncPipe, NgIf, FormsModule],
  templateUrl: './chat-input.html',
  styleUrl: './chat-input.css',
})
export class ChatInputComponent implements OnInit {
  @ViewChild('textareaRef') textareaRef!: ElementRef<HTMLTextAreaElement>;

  state$!: Observable<CopilotState>;
  text = '';

  constructor(private readonly copilot: CopilotService) {}

  ngOnInit(): void {
    this.state$ = this.copilot.state$;
  }

  onSend(): void {
    const trimmed = this.text.trim();
    if (!trimmed) return;
    this.copilot.sendMessage(trimmed);
    this.text = '';
    this.resetTextarea();
  }

  onStop(): void {
    this.copilot.stopGeneration();
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.onSend();
    }
  }

  onInput(): void {
    const el = this.textareaRef?.nativeElement;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = Math.min(el.scrollHeight, 120) + 'px';
  }

  private resetTextarea(): void {
    const el = this.textareaRef?.nativeElement;
    if (el) {
      el.style.height = 'auto';
    }
  }
}
