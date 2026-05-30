export interface ImportRowError {
  row: number;
  message: string;
}

// Outcome of a partial CSV import (mirrors the server's ImportResult).
export interface ImportResult {
  created: number;
  updated: number;
  errors: ImportRowError[];
}
