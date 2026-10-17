import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { ChipGroup } from './chip-group';

describe('ChipGroup', () => {
  it('選択肢をラジオとして並べ、選択中を aria-checked で示し、クリックで通知する', async () => {
    const f = TestBed.createComponent(ChipGroup);
    f.componentRef.setInput('options', [{ value: null, label: 'g' }, { value: 101, label: '本' }]);
    f.componentRef.setInput('value', 101);
    f.componentRef.setInput('label', '単位');
    const picked: unknown[] = [];
    f.componentInstance.changed.subscribe((v) => picked.push(v));
    await f.whenStable();
    const el = f.nativeElement as HTMLElement;
    const radios = [...el.querySelectorAll('button[role=radio]')] as HTMLButtonElement[];
    expect(radios.map((b) => b.textContent?.trim())).toEqual(['g', '本']);
    expect(radios.map((b) => b.getAttribute('aria-checked'))).toEqual(['false', 'true']);
    expect(el.querySelector('[role=radiogroup]')?.getAttribute('aria-label')).toBe('単位');
    radios[0]?.click();
    expect(picked).toEqual([null]);
  });
});
