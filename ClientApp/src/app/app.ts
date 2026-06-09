import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppFooterComponent } from './shared/app-footer.component';
import { ToastContainerComponent } from './shared/toast-container.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainerComponent, AppFooterComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Sticky-footer shell: the routed page grows to fill the viewport and the footer rests
  // beneath it, so a page that fits the screen produces no scrollbar (the footer no longer
  // stacks its height on top of a full-height page). See the host rule in styles.css.
  template: `<div class="flex min-h-screen flex-col">
      <router-outlet />
      <app-footer />
    </div>
    <app-toast-container />`,
})
export class App {}
