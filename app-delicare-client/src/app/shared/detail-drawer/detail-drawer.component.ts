import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  HostListener,
  Input,
  Output,
} from '@angular/core';

export interface DrawerTab {
  key: string;
  label: string;
}

// Right-side detail drawer with tabs (D-36: only screens that need a deep view – customer 360°,
// supplier, recipe, order, PO, batch, trip). Content per tab is projected by the screen:
// <app-detail-drawer [open]="open" [title]="row?.name" [tabs]="tabs" [(activeTab)]="tab" (closed)="open=false">
//   @if (tab === 'info') { … }
//   <ng-container drawer-footer><button class="btn btn-primary">Sửa</button></ng-container>
// </app-detail-drawer>
@Component({
  selector: 'app-detail-drawer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './detail-drawer.component.html',
})
export class DetailDrawerComponent {
  @Input() public open = false;
  @Input() public title = '';
  @Input() public subtitle = '';
  @Input() public tabs: DrawerTab[] = [];
  @Input() public activeTab = '';
  @Input() public width = '720px';
  @Output() public activeTabChange = new EventEmitter<string>();
  @Output() public closed = new EventEmitter<void>();

  public selectTab(key: string): void {
    this.activeTab = key;
    this.activeTabChange.emit(key);
  }

  public close(): void {
    this.closed.emit();
  }

  @HostListener('document:keydown.escape')
  public onEscape(): void {
    if (this.open) {
      this.close();
    }
  }
}
