export type MessageRole = 'user' | 'assistant';

export interface ChatMessage {
  id: string;
  role: MessageRole;
  content: string;
  timestamp: Date;
  isStreaming?: boolean;
}

export interface ModelOption {
  id: string;
  displayName: string;
  isDefault: boolean;
}

export interface CopilotState {
  isOpen: boolean;
  isMinimized: boolean;
  messages: ChatMessage[];
  isTyping: boolean;
  isStreaming: boolean;
  availableModels: ModelOption[];
  selectedModelId: string | null;
}

export interface SuggestedQuestion {
  id: string;
  text: string;
  icon: string;
}

export const SUGGESTED_QUESTIONS: SuggestedQuestion[] = [
  { id: '1', text: 'Show summary for current year', icon: '📊' },
  { id: '2', text: 'What is the total sales amount?', icon: '💰' },
  { id: '3', text: 'Show top groups by GP2', icon: '📈' },
  { id: '4', text: 'Compare budget vs actual amount', icon: '🔍' },
  { id: '5', text: 'Show monthly breakdown', icon: '📅' },
  { id: '6', text: 'Which accounts have highest expenses?', icon: '💡' },
  { id: '7', text: 'Show data for a specific month', icon: '🗓️' },
];
