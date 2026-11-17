import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges } from '@angular/core';
import { MetaDataService } from '../../services/meta-data.service';

// Coloured status label (D-36). Label from api/MetaData/GetStatuses (cached), colour from the code.
// <app-status-badge key="Recipe.Status" [code]="row.status"></app-status-badge>
@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `@if (code) {<span class="dc-badge" [ngClass]="color">{{ text }}</span>}`,
})
export class StatusBadgeComponent implements OnChanges {
  @Input() public key = '';
  @Input() public code?: string | null;
  // Optional fixed label (skips the lookup)
  @Input() public label?: string;

  public text = '';
  public color = 'gray';

  constructor(private metaDataService: MetaDataService) {}

  public ngOnChanges(): void {
    this.color = this.metaDataService.statusColor(this.code);
    this.text = this.label ?? this.code ?? '';
    if (this.label || !this.key || !this.code) {
      return;
    }
    const code = this.code;
    this.metaDataService.getStatusSet(this.key).subscribe((items) => {
      if (code === this.code) {
        this.text = items.find((x) => x.code?.toLowerCase() === code.toLowerCase())?.label ?? code;
      }
    });
  }
}
