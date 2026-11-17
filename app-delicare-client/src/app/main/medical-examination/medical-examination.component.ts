import { Component, OnInit, Renderer2 } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import {
  checkClassActive,
  notificationCommon,
  permission,
} from 'src/app/helpers/common-helper';
import {
  ADD_MODAL_TITLE,
  ERROR,
  STRING_EMPTY,
  SUCCESS,
  UPDATE_MODAL_TITLE,
} from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { MedicalExaminationService } from 'src/app/services/medical-examination.service';
import { ModalService } from 'src/app/shared/modal.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { TableState } from 'src/app/shared/table/table-state';
import { MedicalExaminationModalComponent } from './medical-examination-modal/medical-examination-modal.component';

@Component({
  selector: 'app-medical-examination',
  templateUrl: './medical-examination.component.html',
})
export class MedicalExaminationComponent implements OnInit {
  public listData: any[] = [];
  public searchForm!: UntypedFormGroup;
  public filterForm!: UntypedFormGroup;
  public isLoading = false;
  public table = new TableState();
  public permission: any;

  appliedFilters: any = {};
  // Số lượng điều kiện đang áp dụng
  activeFilterCount = 0;

  constructor(
    private authenticationService: AuthenticationService,
    private medicalExaminationService: MedicalExaminationService,
    private formBuilder: UntypedFormBuilder,
    private notifyService: NotifyService,
    private modalService: ModalService,
    private router: Router,
    private renderer: Renderer2
  ) {}

  public ngOnInit(): void {
    checkClassActive(
      this.router,
      this.renderer,
      this.authenticationService.userValue?.listMenu
    );
    this.permission = permission(
      this.router.url,
      this.authenticationService.userValue?.listMenu
    );
    this.searchForm = this.formBuilder.group({
      searchText: [STRING_EMPTY],
    });
    this.filterForm = this.formBuilder.group({
      price: this.formBuilder.group({ from: [null], to: [null] }),
      createdDate: this.formBuilder.group({ from: [''], to: [''] }),
    });
    this.appliedFilters = this.filterForm.getRawValue();
    if (this.permission?.allowAccess) {
      this.getData();
    }
  }

  private getData(): void {
    this.isLoading = true;
    const { searchText } = this.searchForm.value;
    const { price, createdDate } = this.appliedFilters;
    const param = {
      ...this.table.toParams(),
      searchText: searchText ?? STRING_EMPTY,
      priceFrom: price?.from,
      priceTo: price?.to,
      createdDateFrom: createdDate?.from,
      createdDateTo: createdDate?.to,
    };
    this.medicalExaminationService.getAll(param).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.table.total = data.totalRecords;
          this.listData = data.listData;
        }
      },
      () => {
        this.isLoading = false;
        notificationCommon(this.notifyService, ERROR);
      },
      () => {
        this.isLoading = false;
      }
    );
  }

  public search(): void {
    this.table.page = 1;
    this.getData();
  }

  public sort(field: string): void {
    this.table.toggleSort(field);
    this.getData();
  }

  public changePage(page: number): void {
    this.table.page = page;
    this.getData();
  }

  public changePageSize(size: number): void {
    this.table.pageSize = size;
    this.table.page = 1;
    this.getData();
  }

  public openModal(data?: any): void {
    this.modalService
      .openForResult(MedicalExaminationModalComponent, {
        title: data ? UPDATE_MODAL_TITLE : ADD_MODAL_TITLE,
        data,
        size: 'lg',
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.getData();
        }
      });
  }

  public async delete(id: any): Promise<void> {
    if (!(await this.notifyService.confirmDelete())) {
      return;
    }
    this.isLoading = true;
    this.medicalExaminationService.delete(id).subscribe(
      (result) => {
        if (result.responseCode === 200) {
          notificationCommon(
            this.notifyService,
            SUCCESS,
            result.responseMessage
          );
          if (this.listData.length === 1 && this.table.page > 1) {
            this.table.page--;
          }
          this.getData();
        } else {
          notificationCommon(
            this.notifyService,
            ERROR,
            result.responseMessage
          );
        }
      },
      () => {
        this.isLoading = false;
        notificationCommon(this.notifyService, ERROR);
      },
      () => {
        this.isLoading = false;
      }
    );
  }

  public reloadPage(): void {
    location.reload();
  }
  // Thêm hàm này vào class của Component
  public clearSearch(): void {
    // Reset giá trị ô input về rỗng
    this.searchForm.patchValue({
      searchText: ''
    });
    
    // Gọi lại hàm reloadPage hoặc search để làm mới danh sách
    this.reloadPage(); 
  }

    // Khi mở Popup
  onFilterOpened(): void {

    // Hiển thị lại điều kiện đã áp dụng
    this.filterForm.reset(
      structuredClone(this.appliedFilters)
    );
  }
  // Khi đóng Popup mà không áp dụng
  onFilterDismissed(): void {

    this.filterForm.reset(
      structuredClone(this.appliedFilters)
    );
  }
  // Khi nhấn Áp dụng
  onFilterApply(): void {
    if (this.filterForm.invalid) {
      this.filterForm.markAllAsTouched();
      return;
    }

    const values = this.filterForm.getRawValue();

    // Kiểm tra khoảng giá / khoảng ngày
    const ranges = [
      { label: 'giá', value: values.price },
      { label: 'ngày tạo', value: values.createdDate },
    ];
    for (const range of ranges) {
      if (
        this.hasValue(range.value.from) &&
        this.hasValue(range.value.to) &&
        range.value.from > range.value.to
      ) {
        notificationCommon(
          this.notifyService,
          ERROR,
          `Khoảng ${range.label} không hợp lệ: giá trị "từ" lớn hơn "đến"`
        );
        return;
      }
    }

    this.appliedFilters = structuredClone(values);
    this.activeFilterCount = this.countFilters(values);
    this.table.page = 1;
    this.getData();
  }

  private hasValue(value: any): boolean {
    return value !== null && value !== undefined && value !== '';
  }

  // Đếm số điều kiện đang lọc (mỗi khoảng có "từ" hoặc "đến" tính là 1)
  private countFilters(values: any): number {
    return Object.values(values).filter(
      (range: any) => this.hasValue(range?.from) || this.hasValue(range?.to)
    ).length;
  }

  // Xóa bộ lọc
  onFilterClear(): void {
    this.filterForm.reset({
      price: { from: null, to: null },
      createdDate: { from: '', to: '' },
    });
    this.appliedFilters = this.filterForm.getRawValue();
    this.activeFilterCount = 0;
    this.table.page = 1;
    this.getData();
  }
}
