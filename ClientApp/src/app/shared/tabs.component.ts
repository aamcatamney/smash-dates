import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';

export interface TabDef {
  id: string;
  label: string;
}

// Accessible tab bar. The active tab lives in a query param (default `tab`) so it survives
// refresh and is shareable. Pages read `active()` (via a template ref) to render the matching
// panel, which should be a role="tabpanel" with id="panel-{id}" and aria-labelledby="tab-{id}".
@Component({
  selector: 'app-tabs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="tablist" class="mt-6 flex flex-wrap gap-1 border-b border-slate-200 dark:border-slate-800">
      @for (t of tabs(); track t.id) {
        <button
          type="button"
          role="tab"
          [id]="'tab-' + t.id"
          [attr.aria-selected]="active() === t.id"
          [attr.aria-controls]="'panel-' + t.id"
          [tabindex]="active() === t.id ? 0 : -1"
          (click)="select(t.id)"
          (keydown)="onKey($event)"
          [class]="
            active() === t.id
              ? 'border-b-2 border-slate-900 px-4 py-2 font-mono text-sm font-medium text-slate-900 dark:border-amber-400 dark:text-slate-100'
              : 'border-b-2 border-transparent px-4 py-2 font-mono text-sm text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-200'
          "
        >
          {{ t.label }}
        </button>
      }
    </div>
  `,
})
export class TabsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly tabs = input.required<TabDef[]>();
  readonly param = input('tab');

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  // The active tab id: the query param if it names a known tab, else the first tab.
  readonly active = computed(() => {
    const ids = this.tabs().map((t) => t.id);
    const requested = this.queryParams().get(this.param());
    return requested && ids.includes(requested) ? requested : (ids[0] ?? '');
  });

  protected select(id: string): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { [this.param()]: id },
      queryParamsHandling: 'merge',
    });
  }

  protected onKey(event: KeyboardEvent): void {
    const ids = this.tabs().map((t) => t.id);
    const current = ids.indexOf(this.active());
    let next = -1;
    switch (event.key) {
      case 'ArrowRight':
        next = (current + 1) % ids.length;
        break;
      case 'ArrowLeft':
        next = (current - 1 + ids.length) % ids.length;
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = ids.length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.select(ids[next]);
    this.host.nativeElement.querySelector<HTMLButtonElement>('#tab-' + ids[next])?.focus();
  }
}
