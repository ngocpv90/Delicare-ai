import { Injectable } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import Swal from 'sweetalert2';
import {
  CONFIRM_DELETE,
  ERROR,
  INTERNAL_SERVER_ERROR,
  NOTIFICATION_TITLE,
  SUCCESS,
  WARNING,
} from '../helpers/constant-helper';

// W3CRM replacement for NzNotificationService / nz-popconfirm (toastr + sweetalert2)
@Injectable({
  providedIn: 'root',
})
export class NotifyService {
  constructor(private toastr: ToastrService) {}

  public show(type: string, message?: string): void {
    const text = message || INTERNAL_SERVER_ERROR;
    const options = { positionClass: 'toast-top-right', closeButton: true };
    if (type === SUCCESS) {
      this.toastr.success(text, NOTIFICATION_TITLE, options);
    } else if (type === WARNING) {
      this.toastr.warning(text, NOTIFICATION_TITLE, options);
    } else if (type === ERROR) {
      this.toastr.error(text, NOTIFICATION_TITLE, options);
    } else {
      this.toastr.info(text, NOTIFICATION_TITLE, options);
    }
  }

  public success(message?: string): void {
    this.show(SUCCESS, message);
  }

  public error(message?: string): void {
    this.show(ERROR, message);
  }

  public async confirmDelete(message = CONFIRM_DELETE): Promise<boolean> {
    const result = await Swal.fire({
      title: NOTIFICATION_TITLE,
      text: message,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Đồng ý',
      cancelButtonText: 'Hủy',
      confirmButtonColor: 'var(--primary)',
      cancelButtonColor: '#6e7881',
      reverseButtons: true,
    });
    return result.isConfirmed;
  }

  // Yes / no question for a business action (gửi duyệt, tính lại…)
  public async confirm(message: string, confirmText = 'Đồng ý'): Promise<boolean> {
    const result = await Swal.fire({
      title: NOTIFICATION_TITLE,
      text: message,
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: confirmText,
      cancelButtonText: 'Hủy',
      confirmButtonColor: 'var(--primary)',
      cancelButtonColor: '#6e7881',
      reverseButtons: true,
    });
    return result.isConfirmed;
  }

  // Asks for one value (text / textarea / date). Returns null when cancelled.
  public async prompt(options: {
    title: string;
    label?: string;
    input?: 'text' | 'textarea' | 'date';
    value?: string;
    required?: boolean;
    confirmText?: string;
  }): Promise<string | null> {
    const result = await Swal.fire({
      title: options.title,
      input: options.input ?? 'textarea',
      inputLabel: options.label,
      inputValue: options.value ?? '',
      showCancelButton: true,
      confirmButtonText: options.confirmText ?? 'Đồng ý',
      cancelButtonText: 'Hủy',
      confirmButtonColor: 'var(--primary)',
      cancelButtonColor: '#6e7881',
      reverseButtons: true,
      inputValidator: (value: any) => (options.required && !String(value ?? '').trim() ? 'Vui lòng nhập dữ liệu' : null),
    });
    return result.isConfirmed ? String(result.value ?? '') : null;
  }
}
