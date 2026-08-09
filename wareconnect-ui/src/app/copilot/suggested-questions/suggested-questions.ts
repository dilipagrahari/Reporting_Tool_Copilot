import { Component, EventEmitter, Output } from '@angular/core';
import { NgFor } from '@angular/common';
import { SUGGESTED_QUESTIONS, SuggestedQuestion } from '../copilot.models';

@Component({
  selector: 'app-suggested-questions',
  standalone: true,
  imports: [NgFor],
  templateUrl: './suggested-questions.html',
  styleUrl: './suggested-questions.css',
})
export class SuggestedQuestionsComponent {
  @Output() selected = new EventEmitter<string>();

  readonly questions: SuggestedQuestion[] = SUGGESTED_QUESTIONS;

  onSelect(question: SuggestedQuestion): void {
    this.selected.emit(question.text);
  }
}
