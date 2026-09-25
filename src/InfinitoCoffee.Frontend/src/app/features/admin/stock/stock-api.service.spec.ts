import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APP_RUNTIME_CONFIG } from '../../../core/config/app-runtime-config';
import { StockApiService } from './stock-api.service';

describe('StockApiService retry safety', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
    { provide: APP_RUNTIME_CONFIG, useValue: { apiBaseUrl: 'https://api.test' } },
  ] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('reuses an operation id after failure and uses a new one after success', async () => {
    const api = TestBed.inject(StockApiService); const http = TestBed.inject(HttpTestingController);
    const body = { location: 'Factory', lines: [{ itemId: 'eggs', quantity: 20, unit: 'unit' }] };
    const failed = api.record('receipts', body).catch(() => undefined);
    const request = http.expectOne('https://api.test/api/stock/receipts');
    const operationId = request.request.body.operationId;
    request.error(new ProgressEvent('error')); await failed;
    const retry = api.record('receipts', body);
    const retried = http.expectOne('https://api.test/api/stock/receipts');
    expect(retried.request.body.operationId).toBe(operationId);
    retried.flush(null); await retry;
    const next = api.record('receipts', body);
    const repeated = http.expectOne('https://api.test/api/stock/receipts');
    expect(repeated.request.body.operationId).not.toBe(operationId);
    repeated.flush(null); await next;
  });

  it('uses a different id if the administrator corrects a failed operation', async () => {
    const api = TestBed.inject(StockApiService); const http = TestBed.inject(HttpTestingController);
    const failed = api.record('production', { quantity: 100 }).catch(() => undefined);
    const request = http.expectOne('https://api.test/api/stock/production');
    const operationId = request.request.body.operationId;
    request.flush({}, { status: 409, statusText: 'Conflict' }); await failed;
    const corrected = api.record('production', { quantity: 20 });
    const next = http.expectOne('https://api.test/api/stock/production');
    expect(next.request.body.operationId).not.toBe(operationId);
    next.flush(null); await corrected;
  });
});
