import { Component, OnInit } from '@angular/core';
import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { Observable } from 'rxjs';
import { CopilotState } from '../copilot.models';
import { CopilotService } from '../copilot.service';

@Component({
  selector: 'app-chat-header',
  standalone: true,
  imports: [AsyncPipe, NgIf, NgFor],
  templateUrl: './chat-header.html',
  styleUrl: './chat-header.css',
})
export class ChatHeaderComponent implements OnInit {
  state$!: Observable<CopilotState>;
  showClearConfirm = false;

  constructor(private readonly copilot: CopilotService) {}

  ngOnInit(): void {
    this.state$ = this.copilot.state$;
  }

  onClose(): void    { this.copilot.close(); }
  onClearClick(): void   { this.showClearConfirm = true; }
  onCancelClear(): void  { this.showClearConfirm = false; }
  onConfirmClear(): void { this.copilot.clearConversation(); this.showClearConfirm = false; }

  onModelChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.copilot.setModel(select.value);
    // Clear conversation so the new model starts fresh
    this.copilot.clearConversation();
  }
}
