import { AsyncPipe, NgIf } from '@angular/common';
import {
  Component,
  HostListener,
  OnInit,
} from '@angular/core';
import { Observable } from 'rxjs';
import { CopilotState } from '../copilot.models';
import { CopilotService } from '../copilot.service';
import { ChatHeaderComponent } from '../chat-header/chat-header';
import { ChatBodyComponent } from '../chat-body/chat-body';
import { ChatInputComponent } from '../chat-input/chat-input';

@Component({
  selector: 'app-copilot-window',
  standalone: true,
  imports: [AsyncPipe, NgIf, ChatHeaderComponent, ChatBodyComponent, ChatInputComponent],
  templateUrl: './copilot-window.html',
  styleUrl: './copilot-window.css',
})
export class CopilotWindowComponent implements OnInit {
  state$!: Observable<CopilotState>;

  constructor(private readonly copilot: CopilotService) {}

  ngOnInit(): void {
    this.state$ = this.copilot.state$;
  }

  onClose(): void {
    this.copilot.close();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.copilot.close();
  }
}
