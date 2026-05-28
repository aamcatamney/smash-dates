import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminHeaderComponent } from '../admin/admin-header.component';

@Component({
  selector: 'app-landing-page',
  imports: [RouterLink, AdminHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen flex flex-col bg-slate-50">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl flex-1 px-4 py-12">
        <section class="grid gap-4 md:grid-cols-2">
          <a
            [routerLink]="['/admin/leagues']"
            class="block rounded-xl border border-slate-200 bg-white p-6 shadow-sm hover:border-slate-400 focus-visible:outline-2 focus-visible:outline-slate-900"
          >
            <h2 class="font-mono text-xs uppercase tracking-wider text-slate-500">/admin/leagues</h2>
            <p class="mt-2 text-2xl font-semibold text-slate-900">Leagues</p>
            <p class="mt-1 text-sm text-slate-600">Configure divisions and league admins.</p>
          </a>

          <a
            [routerLink]="['/admin/clubs']"
            class="block rounded-xl border border-slate-200 bg-white p-6 shadow-sm hover:border-slate-400 focus-visible:outline-2 focus-visible:outline-slate-900"
          >
            <h2 class="font-mono text-xs uppercase tracking-wider text-slate-500">/admin/clubs</h2>
            <p class="mt-2 text-2xl font-semibold text-slate-900">Clubs</p>
            <p class="mt-1 text-sm text-slate-600">Manage clubs, club admins, and league memberships.</p>
          </a>
        </section>
      </main>
    </div>
  `,
})
export default class LandingPage {}
