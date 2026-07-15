import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { UploadService } from './upload.service';
import { environment } from '../../../environments/environment';

describe('UploadService', () => {
  let service: UploadService;
  let http: HttpTestingController;
  const url = `${environment.apiBaseUrl}/api/v1/uploads/image`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), UploadService],
    });
    service = TestBed.inject(UploadService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uploadImage posts FormData and returns the url', () => {
    const file = new File(['bytes'], 'poster.png', { type: 'image/png' });
    service.uploadImage(file).subscribe(r => expect(r.url).toBe('http://localhost:5222/uploads/a.png'));

    const req = http.expectOne(url);
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    expect((req.request.body as FormData).get('file')).toBeInstanceOf(File);
    req.flush({ url: 'http://localhost:5222/uploads/a.png' });
  });

  it('maps an error response to a string message', () => {
    service.uploadImage(new File(['x'], 'x.png', { type: 'image/png' }))
      .subscribe({ error: (e: string) => expect(e).toBe('Image must be 5 MB or smaller.') });
    http.expectOne(url).flush({ error: 'Image must be 5 MB or smaller.' }, { status: 400, statusText: 'Bad Request' });
  });
});
