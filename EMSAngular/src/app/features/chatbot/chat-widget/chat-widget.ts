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
    this.chatbot.stream(text, this.conversationId).subscribe({
      next: (e) => {
        if (e.type === 'token') assistant.text += e.text ?? '';
        else if (e.type === 'error') assistant.text += (e.text ?? '');
      },
      error: () => {
        assistant.text += ' (connection error)';
        this.busy.set(false);
      },
      complete: () => this.busy.set(false),
    });
  }
}
