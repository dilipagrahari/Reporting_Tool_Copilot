import {
  AfterViewChecked,
  Component,
  ElementRef,
  OnInit,
  ViewChild,
} from '@angular/core';
import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { Observable } from 'rxjs';
import { CopilotState } from '../copilot.models';
import { CopilotService } from '../copilot.service';
import { ChatMessageComponent } from '../chat-message/chat-message';
import { SuggestedQuestionsComponent } from '../suggested-questions/suggested-questions';
import { TypingIndicatorComponent } from '../typing-indicator/typing-indicator';

@Component({
  selector: 'app-chat-body',
  standalone: true,
  imports: [
    AsyncPipe,
    NgIf,
    NgFor,
    ChatMessageComponent,
    SuggestedQuestionsComponent,
    TypingIndicatorComponent,
  ],
  templateUrl: './chat-body.html',
  styleUrl: './chat-body.css',
})
export class ChatBodyComponent implements OnInit, AfterViewChecked {
  @ViewChild('scrollAnchor') private scrollAnchor!: ElementRef;

  state$!: Observable<CopilotState>;
  private _shouldScroll = false;

  constructor(private readonly copilot: CopilotService) {}

  ngOnInit(): void {
    this.state$ = this.copilot.state$;
    this.copilot.state$.subscribe(() => {
      this._shouldScroll = true;
    });
  }

  ngAfterViewChecked(): void {
    if (this._shouldScroll) {
      this._scrollToBottom();
      this._shouldScroll = false;
    }
  }

  onSuggestionSelected(text: string): void {
    this.copilot.sendMessage(text);
  }

  trackById(_: number, msg: import('../copilot.models').ChatMessage): string {
    return msg.id;
  }

  private _scrollToBottom(): void {
    try {
      this.scrollAnchor?.nativeElement?.scrollIntoView({ behavior: 'smooth' });
    } catch (_) {}
  }
}
