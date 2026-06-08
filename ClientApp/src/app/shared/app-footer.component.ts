import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { VersionApi } from '../core/version/version.api';

// Global footer shown on every page (authed + anonymous). Renders the build-stamped
// CalVer version served by GET /api/version; stays silent until the version resolves.
@Component({
  selector: 'app-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (version(); as v) {
      <footer
        class="border-t border-slate-200 bg-white px-4 py-3 text-center dark:border-slate-800 dark:bg-slate-900"
      >
        <span class="font-mono text-xs text-slate-500 dark:text-slate-400">
          smash-dates
          <span class="text-slate-400 dark:text-slate-500">·</span>
          {{ v }}
        </span>
      </footer>
    }
  `,
})
export class AppFooterComponent {
  private readonly api = inject(VersionApi);
  readonly version = toSignal(this.api.get().pipe(map((v) => v.version)));
}
