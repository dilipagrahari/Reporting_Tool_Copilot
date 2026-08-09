import { Injectable, OnDestroy } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { ChatMessage, CopilotState, ModelOption } from './copilot.models';

function generateId(): string {
  return Math.random().toString(36).slice(2, 11);
}

interface StreamingChunk {
  conversationId: string;
  delta?: string;
  isDone: boolean;
  error?: string;
  usage?: { promptTokens: number; completionTokens: number; totalTokens: number; model: string };
}

@Injectable({ providedIn: 'root' })
export class CopilotService implements OnDestroy {
  private readonly _destroy$ = new Subject<void>();
  private readonly _apiUrl   = 'http://localhost:5256/api/copilot/chat';
  private readonly _modelsUrl = 'http://localhost:5256/api/copilot/models';

  private _conversationId: string | null = null;
  private _abortController: AbortController | null = null;

  private readonly _state$ = new BehaviorSubject<CopilotState>({
    isOpen: false,
    isMinimized: false,
    messages: [],
    isTyping: false,
    isStreaming: false,
    availableModels: [],
    selectedModelId: null,
  });

  readonly state$: Observable<CopilotState> = this._state$.asObservable();

  constructor() {
    this._loadModels();
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
    this._abortController?.abort();
  }

  setModel(modelId: string): void {
    this._state$.next({ ...this._state$.value, selectedModelId: modelId });
  }

  toggle(): void {
    const s = this._state$.value;
    this._state$.next({ ...s, isOpen: !s.isOpen, isMinimized: false });
  }

  open(): void {
    this._state$.next({ ...this._state$.value, isOpen: true, isMinimized: false });
  }

  close(): void {
    this._abortController?.abort();
    this._state$.next({ ...this._state$.value, isOpen: false, isTyping: false, isStreaming: false });
  }

  minimize(): void {
    const s = this._state$.value;
    this._state$.next({ ...s, isMinimized: !s.isMinimized });
  }

  clearConversation(): void {
    this._abortController?.abort();
    this._conversationId = null;
    this._state$.next({ ...this._state$.value, messages: [], isTyping: false, isStreaming: false });
  }

  sendMessage(text: string): void {
    if (!text.trim() || this._state$.value.isStreaming) return;

    const userMsg: ChatMessage = {
      id: generateId(),
      role: 'user',
      content: text.trim(),
      timestamp: new Date(),
    };

    const currentMessages = [...this._state$.value.messages, userMsg];
    this._state$.next({ ...this._state$.value, messages: currentMessages, isTyping: true });

    this._streamFromApi(text.trim());
  }

  stopGeneration(): void {
    this._abortController?.abort();
    const s = this._state$.value;
    const messages = s.messages.map((m) => m.isStreaming ? { ...m, isStreaming: false } : m);
    this._state$.next({ ...s, messages, isTyping: false, isStreaming: false });
  }

  private _streamFromApi(userText: string): void {
    this._abortController = new AbortController();
    const signal = this._abortController.signal;

    const assistantMsgId = generateId();
    const assistantMsg: ChatMessage = {
      id: assistantMsgId,
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      isStreaming: true,
    };

    const s = this._state$.value;
    this._state$.next({
      ...s,
      isTyping: false,
      isStreaming: true,
      messages: [...s.messages, assistantMsg],
    });

    const body = JSON.stringify({
      conversationId: this._conversationId,
      userId: '1',
      message: userText,
      modelOverride: this._state$.value.selectedModelId ?? undefined,
      screenContext: { currentPage: 'Dashboard' },
    });

    fetch(this._apiUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
      body,
      signal,
    })
      .then(async (res) => {
        if (!res.ok || !res.body) throw new Error(`HTTP ${res.status}`);

        const reader = res.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n\n');
          buffer = lines.pop() ?? '';

          for (const block of lines) {
            for (const line of block.split('\n')) {
              if (line.startsWith('data: ')) {
                const jsonStr = line.slice(6).trim();
                if (!jsonStr || jsonStr === '{}') continue;

                try {
                  const chunk: StreamingChunk = JSON.parse(jsonStr);

                  if (chunk.conversationId && !this._conversationId) {
                    this._conversationId = chunk.conversationId;
                  }

                  const cur = this._state$.value;

                  if (chunk.isDone) {
                    const msgs = cur.messages.map((m) =>
                      m.id === assistantMsgId ? { ...m, isStreaming: false } : m
                    );
                    this._state$.next({ ...cur, messages: msgs, isStreaming: false, isTyping: false });
                    return;
                  }

                  if (chunk.delta) {
                    const msgs = cur.messages.map((m) =>
                      m.id === assistantMsgId ? { ...m, content: m.content + chunk.delta } : m
                    );
                    this._state$.next({ ...cur, messages: msgs });
                  }
                } catch {
                  // malformed chunk â€” skip
                }
              }
            }
          }
        }
      })
      .catch((err) => {
        if (err?.name === 'AbortError') return;
        console.error('Copilot SSE error:', err);

        const cur = this._state$.value;
        const msgs = cur.messages.map((m) =>
          m.id === assistantMsgId
            ? { ...m, content: m.content || 'Unable to connect to Reporting Tool Copilot. Please ensure the API is running.', isStreaming: false }
            : m
        );
        this._state$.next({ ...cur, messages: msgs, isStreaming: false, isTyping: false });
      });
  }

  private _loadModels(): void {
    fetch(this._modelsUrl)
      .then((r) => r.json())
      .then((models: ModelOption[]) => {
        const def = models.find((m) => m.isDefault)?.id ?? models[0]?.id ?? null;
        this._state$.next({ ...this._state$.value, availableModels: models, selectedModelId: def });
      })
      .catch(() => { /* silent — model picker just won't show */ });
  }
}
