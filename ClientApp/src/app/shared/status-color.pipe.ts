import { Pipe, PipeTransform } from '@angular/core';

// Maps a domain status string to Tailwind badge colour classes, so season / match /
// membership states are scannable at a glance instead of uniform grey.
@Pipe({ name: 'statusColor' })
export class StatusColorPipe implements PipeTransform {
  private static readonly map: Record<string, string> = {
    // Season + match lifecycle
    Draft: 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200',
    Scheduling: 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300',
    Proposed: 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300',
    Active: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
    Closed: 'bg-slate-300 text-slate-600 dark:bg-slate-700 dark:text-slate-300',
    Confirmed: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
    Played: 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300',
    Postponed: 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300',
    Rejected: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300',
    // Membership
    Pending: 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300',
    Accepted: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
    Declined: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300',
    Withdrawn: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300',
    Expelled: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300',
  };

  transform(status: string | null | undefined): string {
    return (
      (status && StatusColorPipe.map[status]) ||
      'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300'
    );
  }
}
