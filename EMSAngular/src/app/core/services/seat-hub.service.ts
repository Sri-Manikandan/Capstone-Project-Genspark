import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';

export type SeatStatus = 'reserved' | 'released' | 'booked';
export interface SeatUpdate {
  seatId: number;
  status: SeatStatus;
}

@Injectable({ providedIn: 'root' })
export class SeatHubService {
  private connection?: HubConnection;
  private updateSignal = signal<SeatUpdate | null>(null);
  readonly lastUpdate = this.updateSignal.asReadonly();

  handleSeatEvent(status: SeatStatus, seatId: number): void {
    this.updateSignal.set({ seatId, status });
  }

  async connect(): Promise<void> {
    if (this.connection) return;
    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/seats`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('SeatReserved', (seatId: number) =>
      this.handleSeatEvent('reserved', seatId)
    );
    this.connection.on('SeatReleased', (seatId: number) =>
      this.handleSeatEvent('released', seatId)
    );
    this.connection.on('SeatBooked', (seatId: number) =>
      this.handleSeatEvent('booked', seatId)
    );

    await this.connection.start();
  }

  async joinEvent(eventId: number): Promise<void> {
    await this.connect();
    await this.connection!.invoke('JoinEventRoom', eventId);
  }

  async leaveEvent(eventId: number): Promise<void> {
    if (!this.connection) return;
    await this.connection.invoke('LeaveEventRoom', eventId);
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = undefined;
  }
}
