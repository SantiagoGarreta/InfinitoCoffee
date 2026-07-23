import { TestBed } from '@angular/core/testing';

import { ConnectionStatusComponent } from './connection-status.component';

describe('ConnectionStatusComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConnectionStatusComponent],
    }).compileComponents();
  });

  it('renders the connected label from the realtime state input', () => {
    const fixture = TestBed.createComponent(ConnectionStatusComponent);
    fixture.componentRef.setInput('state', 'connected');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Conectado');
  });

  it('renders the disconnected label and retry action from the realtime state input', () => {
    const fixture = TestBed.createComponent(ConnectionStatusComponent);
    fixture.componentRef.setInput('state', 'disconnected');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Desconectado');
    expect(fixture.nativeElement.querySelector('button')).toBeTruthy();
  });
});
