import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

export interface ChatEvent {
  type: 'token' | 'tool' | 'error' | 'done';
  text?: string;
  name?: string;
}

@Injectable({ providedIn: 'root' })
export class ChatbotService {
  private auth = inject(AuthService);

  stream(message: string, conversationId: string): Observable<ChatEvent> {
    return new Observable<ChatEvent>((subscriber) => {
      const controller = new AbortController();
      (async () => {
        try {
          if (this.auth.isAccessTokenExpired()) {
            try {
              await firstValueFrom(this.auth.refreshShared());
            } catch (err) {
              subscriber.error(err);
              return;
            }
          }
          // Dev: apiBaseUrl is '' -> same-origin '/ai/chat' via the ng proxy.
          // Prod: apiBaseUrl is the AKS ingress URL (SWA cannot proxy to AKS).
          const resp = await fetch(`${environment.apiBaseUrl}/ai/chat`, {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${this.auth.accessToken() ?? ''}`,
            },
            body: JSON.stringify({ message, conversationId }),
            signal: controller.signal,
          });
          if (!resp.body) {
            subscriber.error(new Error('No response body'));
            return;
          }
          const reader = resp.body.getReader();
          const decoder = new TextDecoder();
          let buffer = '';
          for (;;) {
            const { value, done } = await reader.read();
            if (done) break;
            buffer += decoder.decode(value, { stream: true });
            const frames = buffer.split('\n\n');
            buffer = frames.pop() ?? '';
            for (const frame of frames) {
              const line = frame.trim();
              if (!line.startsWith('data:')) continue;
              subscriber.next(JSON.parse(line.slice('data:'.length).trim()));
            }
          }
          subscriber.complete();
        } catch (err) {
          if (!controller.signal.aborted) subscriber.error(err);
        }
      })();
      return () => controller.abort();
    });
  }
}
