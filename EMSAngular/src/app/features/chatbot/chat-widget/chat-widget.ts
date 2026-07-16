import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatbotService } from '../../../core/services/chatbot.service';

interface ChatMessage { role: 'user' | 'assistant'; text: string; }

@Component({
  selector: 'ems-chat-widget',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './chat-widget.html',
  styleUrl: './chat-widget.css',
})
export class ChatWidget {
  private chatbot = inject(ChatbotService);

  protected readonly open = signal(false);
  protected readonly busy = signal(false);
  protected readonly toolActivity = signal<string | null>(null);
  messages: ChatMessage[] = [];
  draft = '';
  private conversationId = crypto.randomUUID();

  toggle(): void {
    this.open.update((v) => !v);
  }

  send(): void {
    const text = this.draft.trim();
    if (!text || this.busy()) return;
    this.messages.push({ role: 'user', text });
    this.draft = '';
    const assistant: ChatMessage = { role: 'assistant', text: '' };
    this.messages.push(assistant);
    this.busy.set(true);
    this.toolActivity.set(null);
    let failureReported = false;
    this.chatbot.stream(text, this.conversationId).subscribe({
      next: (e) => {
        if (e.type === 'token') {
          this.toolActivity.set(null);
          assistant.text += e.text ?? '';
        } else if (e.type === 'tool') {
          this.toolActivity.set(`Using ${e.name}…`);
        } else if (e.type === 'error') {
          assistant.text += (e.text ?? '');
          failureReported = true;
        } else if (e.type === 'done') {
          this.toolActivity.set(null);
        }
      },
      error: () => {
        this.toolActivity.set(null);
        if (!failureReported) assistant.text += ' (connection error)';
        this.busy.set(false);
      },
      complete: () => {
        this.toolActivity.set(null);
        this.busy.set(false);
      },
    });
  }
}
