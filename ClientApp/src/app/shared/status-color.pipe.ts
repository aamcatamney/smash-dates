import { Pipe, PipeTransform } from '@angular/core';

// Maps a domain status string to Tailwind badge colour classes, so season / match /
// membership states are scannable at a glance instead of uniform grey.
@Pipe({ name: 'statusColor' })
export class StatusColorPipe implements PipeTransform {
  private static readonly map: Record<string, string> = {
    // Season + match lifecycle
    Draft: 'bg-slate-200 text-slate-700',
    Proposed: 'bg-amber-100 text-amber-800',
    Active: 'bg-emerald-100 text-emerald-800',
    Closed: 'bg-slate-300 text-slate-600',
    Confirmed: 'bg-emerald-100 text-emerald-800',
    Played: 'bg-blue-100 text-blue-800',
    Postponed: 'bg-amber-100 text-amber-800',
    Rejected: 'bg-red-100 text-red-700',
    // Membership
    Pending: 'bg-amber-100 text-amber-800',
    Accepted: 'bg-emerald-100 text-emerald-800',
    Declined: 'bg-red-100 text-red-700',
    Withdrawn: 'bg-red-100 text-red-700',
    Expelled: 'bg-red-100 text-red-700',
  };

  transform(status: string | null | undefined): string {
    return (status && StatusColorPipe.map[status]) || 'bg-slate-100 text-slate-600';
  }
}
