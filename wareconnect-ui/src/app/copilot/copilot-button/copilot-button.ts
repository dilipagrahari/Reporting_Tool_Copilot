import { Component } from '@angular/core';
import { CopilotService } from '../copilot.service';

@Component({
  selector: 'app-copilot-button',
  standalone: true,
  templateUrl: './copilot-button.html',
  styleUrl: './copilot-button.css',
})
export class CopilotButtonComponent {
  constructor(private readonly copilot: CopilotService) {}

  onClick(): void {
    this.copilot.toggle();
  }
}
