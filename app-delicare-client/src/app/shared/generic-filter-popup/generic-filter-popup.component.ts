import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output
} from '@angular/core';

import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-generic-filter-popup',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './generic-filter-popup.component.html',
  styleUrls: ['./generic-filter-popup.component.scss']
})
export class GenericFilterPopupComponent {

  @Input() title = 'Bộ lọc dữ liệu';

  @Input() subtitle =
    'Thiết lập một hoặc nhiều điều kiện để lọc dữ liệu';

  @Input() buttonText = 'Bộ lọc';

  @Input() activeFilterCount = 0;

  @Input() applyDisabled = false;

  @Input() popupWidth = '790px';

  @Output() apply = new EventEmitter<void>();

  @Output() clear = new EventEmitter<void>();

  @Output() opened = new EventEmitter<void>();

  @Output() dismissed = new EventEmitter<void>();

  isOpen = false;

  constructor(
    private elementRef: ElementRef
  ) {}

  // Mở hoặc đóng Popup
  toggle(): void {

    if (this.isOpen) {
      this.close();
      return;
    }

    this.isOpen = true;
    this.opened.emit();
  }

  // Đóng Popup
  close(): void {

    if (!this.isOpen) return;

    this.isOpen = false;
    this.dismissed.emit();
  }

  // Người dùng nhấn Áp dụng
  onApply(): void {

    if (this.applyDisabled) return;

    this.isOpen = false;

    this.apply.emit();
  }

  // Người dùng nhấn Xóa bộ lọc
  onClear(): void {

    this.isOpen = false;

    this.clear.emit();
  }

  // Click ra ngoài Popup
  @HostListener('document:click', ['$event'])
  onOutsideClick(event: MouseEvent): void {

    if (!this.isOpen) return;

    const clickedInside =
      this.elementRef.nativeElement.contains(event.target);

    if (!clickedInside) {
      this.close();
    }
  }

  // Nhấn ESC
  @HostListener('document:keydown.escape')
  onEscape(): void {

    this.close();
  }

}