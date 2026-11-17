import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { saveAs } from 'file-saver';
import { menuUrl, permission, toFormData } from 'src/app/helpers/common-helper';
import { WARNING } from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { ImportExcelService } from 'src/app/services/import-excel.service';
import { NotifyService } from 'src/app/shared/notify.service';

// sys-import – Nhập dữ liệu Excel (FE-3, T49): ImportExcel/GetTemplateList → chọn loại file → GetTemplate (tải mẫu .xlsx)
// → Import (multipart TableName + File) → số dòng thêm / cập nhật / bỏ qua + lỗi từng dòng (listError) + ghi chú (listNote).
// Không có mục ObjectMenu cho màn hình này (route PROPOSED `sys-import`, GAP ghi ở PROGRESS FE-3): chỉ cần đăng nhập;
// mỗi loại file chỉ hiện khi user có cờ Import trên màn hình đích (template.screen, vd. #/crm-customer) – admin: tất cả.
// Mở với ?table=<TableName> để chọn sẵn (nút "Nhập Excel" của menu-dish).
@Component({
  selector: 'app-master-import',
  templateUrl: './master-import.component.html',
})
export class MasterImportComponent implements OnInit {
  @ViewChild('fileInput') public fileInput?: ElementRef<HTMLInputElement>;
  public templates: any[] = [];
  public selected: any = null;
  public file: File | null = null;
  public result: any = null;
  public resultMessage = '';
  public isLoading = false;
  public isLoadingList = false;

  constructor(
    private importExcelService: ImportExcelService,
    private authenticationService: AuthenticationService,
    private notify: NotifyService,
    private route: ActivatedRoute
  ) {}

  public ngOnInit(): void {
    const user = this.authenticationService.userValue;
    const listMenu = user?.listMenu;
    const wanted = this.route.snapshot.queryParamMap.get('table');
    this.isLoadingList = true;
    this.importExcelService.getTemplateList().subscribe({
      next: (res) => {
        this.isLoadingList = false;
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        this.templates = list
          .filter((x) => user?.isAdmin || !!permission(menuUrl(x.screen), listMenu)?.allowImport)
          .map((x) => ({ ...x, screenTitle: this.screenTitle(x.screen, listMenu) }));
        const preset = wanted ? this.templates.find((x) => x.tableName?.toLowerCase() === wanted.toLowerCase()) : null;
        this.select(preset ?? this.templates[0] ?? null);
      },
      error: () => {
        this.isLoadingList = false;
        this.notify.error();
      },
    });
  }

  private screenTitle(screen: string, listMenu?: any[]): string {
    const url = menuUrl(screen);
    const item = (listMenu ?? []).flatMap((x: any) => [x, ...(x.children ?? [])]).find((x: any) => menuUrl(x.metaLink) === url);
    return item?.objectMenuName ?? screen;
  }

  public select(template: any): void {
    this.selected = template;
    this.result = null;
    this.resultMessage = '';
    this.clearFile();
  }

  public fileChanged(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file = input.files?.[0] ?? null;
    this.result = null;
    this.resultMessage = '';
  }

  public clearFile(): void {
    this.file = null;
    if (this.fileInput?.nativeElement) {
      this.fileInput.nativeElement.value = '';
    }
  }

  public downloadTemplate(): void {
    if (!this.selected?.hasTemplate) {
      return;
    }
    this.isLoading = true;
    this.importExcelService.getTemplate(this.selected.tableName).subscribe({
      next: (blob) => {
        this.isLoading = false;
        saveAs(blob, `Mau_nhap_${this.selected.tableName}.xlsx`);
      },
      error: (err) => {
        this.isLoading = false;
        this.notify.error(err?.status === 403 ? 'Bạn không có quyền nhập dữ liệu cho danh mục này' : undefined);
      },
    });
  }

  public upload(): void {
    if (!this.selected || !this.file) {
      this.notify.error('Vui lòng chọn file Excel (.xlsx / .xlsm)');
      return;
    }
    if (!/\.(xlsx|xlsm)$/i.test(this.file.name)) {
      this.notify.error('Chỉ nhận file .xlsx hoặc .xlsm');
      return;
    }
    const formData = toFormData({ tableName: this.selected.tableName });
    formData.append('file', this.file, this.file.name);
    this.isLoading = true;
    this.result = null;
    this.importExcelService.import(formData).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res?.responseCode === 200 && res.data) {
          this.result = res.data;
          this.resultMessage = res.responseMessage;
          if (res.data.listError?.length) {
            this.notify.show(WARNING, res.responseMessage);
          } else {
            this.notify.success(res.responseMessage);
          }
        } else {
          this.notify.error(res?.responseMessage);
        }
      },
      error: (err) => {
        this.isLoading = false;
        if (err?.status === 403) {
          this.notify.error('Bạn không có quyền nhập dữ liệu cho danh mục này');
        } else if (err?.status === 400 && typeof err.error === 'string') {
          this.notify.error(err.error);
        } else {
          this.notify.error();
        }
      },
    });
  }
}
