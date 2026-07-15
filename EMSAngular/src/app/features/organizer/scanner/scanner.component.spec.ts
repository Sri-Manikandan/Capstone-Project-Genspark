import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Html5Qrcode } from 'html5-qrcode';

import { ScannerComponent } from './scanner.component';

describe('ScannerComponent', () => {
  let component: ScannerComponent;
  let fixture: ComponentFixture<ScannerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScannerComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(ScannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('prefers a rear camera when the device has one', async () => {
    vi.spyOn(Html5Qrcode, 'getCameras').mockResolvedValue([
      { id: 'front-1', label: 'FaceTime HD Front Camera' },
      { id: 'back-1', label: 'Back Camera' },
    ]);
    await expect(component['pickCamera']()).resolves.toBe('back-1');
  });

  it('falls back to the only camera on a laptop (front-facing webcam)', async () => {
    vi.spyOn(Html5Qrcode, 'getCameras').mockResolvedValue([
      { id: 'front-1', label: 'Integrated Webcam' },
    ]);
    await expect(component['pickCamera']()).resolves.toBe('front-1');
  });

  it('reports NO_CAMERA when the device exposes none', async () => {
    vi.spyOn(Html5Qrcode, 'getCameras').mockResolvedValue([]);
    await expect(component['pickCamera']()).rejects.toThrow('NO_CAMERA');
  });

  it('maps a blocked-permission error to an actionable message', () => {
    expect(component['cameraErrorMessage']({ name: 'NotAllowedError' })).toContain('permission');
    expect(component['cameraErrorMessage']({ name: 'NotFoundError' })).toContain('No camera');
    expect(component['cameraErrorMessage']({ name: 'NotReadableError' })).toContain('in use');
  });

  it('shows an HTTPS hint and does not start when the browser exposes no camera API', async () => {
    const original = Object.getOwnPropertyDescriptor(navigator, 'mediaDevices');
    Object.defineProperty(navigator, 'mediaDevices', { configurable: true, value: undefined });
    try {
      await component['startScan']();
      expect(component['scanning']()).toBe(false);
      expect(component['error']()).toContain('HTTPS');
    } finally {
      if (original) Object.defineProperty(navigator, 'mediaDevices', original);
    }
  });
});
