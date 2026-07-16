import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatbotService } from '../../../core/services/chatbot.service';

interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
}

/** What the person recognizes, not what the system calls it. */
const TOOL_LABELS: Record<string, string> = {
  search_events: 'Searching events…',
  get_event_details: 'Pulling up event details…',
  get_my_bookings: 'Checking your bookings…',
  get_booking_details: 'Opening that booking…',
  cancel_pending_booking: 'Cancelling the booking…',
};

@Component({
  selector: 'ems-chat-widget',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './chat-widget.html',
  styleUrl: './chat-widget.css',
})
export class ChatWidget {
  private chatbot = inject(ChatbotService);

  private readonly thread = viewChild<ElementRef<HTMLDivElement>>('thread');
  private readonly chatInput = viewChild<ElementRef<HTMLInputElement>>('chatInput');

  protected readonly open = signal(false);
  protected readonly busy = signal(false);
  protected readonly toolActivity = signal('');
  protected readonly suggestions = [
    "What's on this weekend?",
    'Show my bookings',
    'Cancel a booking',
  ];
  messages: ChatMessage[] = [];
  draft = '';
  private conversationId = crypto.randomUUID();

  toggle(): void {
    this.open.update((v) => !v);
    if (this.open()) {
      setTimeout(() => this.chatInput()?.nativeElement.focus());
    }
  }

  ask(suggestion: string): void {
    this.draft = suggestion;
    this.send();
  }

  send(): void {
    const text = this.draft.trim();
    if (!text || this.busy()) return;
    this.messages.push({ role: 'user', text });
    this.draft = '';
    const assistant: ChatMessage = { role: 'assistant', text: '' };
    this.messages.push(assistant);
    this.busy.set(true);
    this.toolActivity.set('');
    this.scrollToEnd();
    let failureReported = false;
    this.chatbot.stream(text, this.conversationId).subscribe({
      next: (e) => {
        if (e.type === 'token') {
          assistant.text += e.text ?? '';
          this.toolActivity.set('');
        } else if (e.type === 'tool') {
          this.toolActivity.set(TOOL_LABELS[e.name ?? ''] ?? 'Working on it…');
        } else if (e.type === 'error') {
          assistant.text += e.text ?? '';
          failureReported = true;
        }
        this.scrollToEnd();
      },
      error: () => {
        if (!failureReported) assistant.text += 'Connection lost. Try again.';
        this.busy.set(false);
        this.toolActivity.set('');
      },
      complete: () => {
        this.busy.set(false);
        this.toolActivity.set('');
        this.scrollToEnd();
      },
    });
  }

  /**
   * Minimal markdown for chat bubbles: bold, italics, inline code, bullets,
   * line breaks. All input is HTML-escaped first, so the produced string is
   * safe for [innerHTML] — no raw user/model HTML ever passes through.
   */
  renderMarkdown(text: string): string {
    const escaped = text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');
    return escaped
      .replace(/^#{1,4}\s+(.+)$/gm, '<strong>$1</strong>')
      .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
      .replace(/(^|\s)\*([^*\s][^*]*)\*/g, '$1<em>$2</em>')
      .replace(/`([^`]+)`/g, '<code>$1</code>')
      .replace(/^[-•]\s+(.+)$/gm, '<span class="li">$1</span>')
      .replace(/^\s*---+\s*$/gm, '')
      .replace(/\n{2,}/g, '<br><br>')
      .replace(/\n/g, '<br>');
  }

  private scrollToEnd(): void {
    setTimeout(() => {
      const el = this.thread()?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }
}
