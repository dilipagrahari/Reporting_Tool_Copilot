import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { CardModule } from 'primeng/card';
import { BadgeModule } from 'primeng/badge';
import { CopilotButtonComponent } from './copilot/copilot-button/copilot-button';
import { CopilotWindowComponent } from './copilot/copilot-window/copilot-window';
import { CopilotService } from './copilot/copilot.service';

interface ReportRow {
  rowID: number;
  year: number | null;
  myobAccount: string | null;
  accountName: string | null;
  accountType: string | null;
  amount: number | null;
  monthName: string | null;
  groupName: string | null;
  itemType: string | null;
  sales: number | null;
  otherExp: number | null;
  gp2: number | null;
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

@Component({
  selector: 'app-root',
  imports: [
    CommonModule, FormsModule,
    CopilotButtonComponent, CopilotWindowComponent,
    ButtonModule, SelectModule, TableModule, TagModule,
    TooltipModule, CardModule, BadgeModule,
  ],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  private readonly apiBaseUrl = 'http://localhost:5256/api/report-data';
  private readonly _destroy$ = new Subject<void>();

  sidebarCollapsed = false;
  copilotOpen = false;

  // ── Inline amount editing ──────────────────────────────────────
  editingRowId: number | null = null;
  editValue: number | null = null;
  isSaving = false;
  saveError = '';
  @ViewChild('amtInput') amtInputRef?: ElementRef<HTMLInputElement>;

  years: number[] = [];
  selectedYear: number | null = null;
  rows: ReportRow[] = [];
  pageNumber = 1;
  pageSize = 20;
  readonly pageSizeOptions = [10, 20, 50, 100];
  totalCount = 0;
  isLoading = false;
  error = '';

  constructor(
    private readonly http: HttpClient,
    private readonly copilotService: CopilotService,
  ) {}

  ngOnInit(): void {
    this.loadYears();
    // Track copilot open state and enforce mutual exclusivity
    this.copilotService.state$.pipe(takeUntil(this._destroy$)).subscribe(state => {
      this.copilotOpen = state.isOpen;
      if (state.isOpen) {
        this.sidebarCollapsed = true;
      }
    });
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  onYearChange(year: number): void {
    this.selectedYear = year;
    this.pageNumber = 1;
    this.loadRows();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.pageNumber = 1;
    this.loadRows();
  }

  goToPreviousPage(): void {
    if (this.pageNumber > 1) {
      this.pageNumber -= 1;
      this.loadRows();
    }
  }

  goToNextPage(): void {
    if (this.pageNumber < this.totalPages) {
      this.pageNumber += 1;
      this.loadRows();
    }
  }

  get totalPages(): number {
    if (this.totalCount === 0) return 1;
    return Math.ceil(this.totalCount / this.pageSize);
  }

  get yearSelectOptions() {
    return this.years.map(y => ({ label: `Data_${y}`, value: y }));
  }

  get pageSizeSelectOptions() {
    return this.pageSizeOptions.map(s => ({ label: String(s), value: s }));
  }

  toggleSidebar(): void {
    this.sidebarCollapsed = !this.sidebarCollapsed;
    if (!this.sidebarCollapsed) {
      // Expanding sidebar — close copilot
      this.copilotService.close();
    }
  }

  trackByRowId(_: number, item: ReportRow): number {
    return item.rowID;
  }

  // ── Amount inline editing ────────────────────────────────────────

  startEditAmount(row: ReportRow): void {
    this.editingRowId = row.rowID;
    this.editValue = row.amount;
    this.saveError = '';
    // Focus input after Angular renders it
    setTimeout(() => this.amtInputRef?.nativeElement?.select(), 0);
  }

  cancelEdit(): void {
    this.editingRowId = null;
    this.editValue = null;
    this.saveError = '';
  }

  saveAmount(row: ReportRow): void {
    if (this.editValue === null || this.selectedYear === null) return;
    if (this.editValue === row.amount) { this.cancelEdit(); return; }

    this.isSaving = true;
    this.saveError = '';

    this.http
      .put<{ rowId: number; year: number; amount: number }>(
        `${this.apiBaseUrl}/${this.selectedYear}/rows/${row.rowID}/amount`,
        { amount: this.editValue }
      )
      .subscribe({
        next: (res) => {
          row.amount = res.amount;   // update in place — no reload needed
          this.isSaving = false;
          this.cancelEdit();
        },
        error: () => {
          this.saveError = 'Save failed. Please try again.';
          this.isSaving = false;
        }
      });
  }

  private loadYears(): void {
    this.isLoading = true;
    this.error = '';

    this.http.get<number[]>(`${this.apiBaseUrl}/years`).subscribe({
      next: (years) => {
        this.years = years;
        this.selectedYear = years.length > 0 ? years[0] : null;
        if (this.selectedYear !== null) {
          this.loadRows();
        } else {
          this.rows = [];
          this.isLoading = false;
        }
      },
      error: () => {
        this.error = 'Unable to load available years.';
        this.isLoading = false;
      }
    });
  }

  private loadRows(): void {
    if (this.selectedYear === null) {
      this.rows = [];
      return;
    }

    this.isLoading = true;
    this.error = '';

    this.http
      .get<PagedResult<ReportRow>>(`${this.apiBaseUrl}/${this.selectedYear}`, {
        params: {
          pageNumber: String(this.pageNumber),
          pageSize: String(this.pageSize)
        }
      })
      .subscribe({
        next: (result) => {
          this.rows = result.items;
          this.totalCount = result.totalCount;
          this.pageNumber = result.pageNumber;
          this.pageSize = result.pageSize;
          this.isLoading = false;
        },
        error: () => {
          this.error = 'Unable to load table data for selected year.';
          this.rows = [];
          this.totalCount = 0;
          this.isLoading = false;
        }
      });
  }
}
