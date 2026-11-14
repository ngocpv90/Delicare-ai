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
import { MasterDataService } from 'src/app/services/master-data.service';
import { ModalService } from 'src/app/shared/modal.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { TableState } from 'src/app/shared/table/table-state';
import { MD_OPTIONS, mdLabel } from '../master-data.constants';
import { MdIngredientModalComponent } from './md-ingredient-modal/md-ingredient-modal.component';

const CONTROLLER = 'Ingredient';

@Component({
  selector: 'app-md-ingredient',
  templateUrl: './md-ingredient.component.html',
})
export class MdIngredientComponent implements OnInit {
  public listData: any[] = [];
  public searchForm!: UntypedFormGroup;
  public isLoading = false;
  public table = new TableState();
  public permission: any;
  public options = MD_OPTIONS;
  public lookup: Record<string, any[]> = {};
  public loadingLookup: Record<string, boolean> = {};
  private readonly filterLookups: { field: string; controller: string }[] = [

  ];

  constructor(
    private authenticationService: AuthenticationService,
    private masterDataService: MasterDataService,
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
    if (this.permission?.allowAccess) {
      this.filterLookups.forEach((x) => this.loadLookup(x.field, x.controller));
      this.getData();
    }
  }

  public label(key: string, value: any): string {
    return mdLabel(key, value);
  }

  private loadLookup(field: string, controller: string): void {
    this.loadingLookup[field] = true;
    this.masterDataService.getAllData(controller).subscribe(
      (res) => {
        this.lookup[field] = res?.listData ?? [];
      },
      () => {
        this.loadingLookup[field] = false;
      },
      () => {
        this.loadingLookup[field] = false;
      }
    );
  }

  private getData(): void {
    this.isLoading = true;
    const param = {
      ...this.table.toParams(),
      ...this.searchForm.value,
    };
    this.masterDataService.getAll(CONTROLLER, param).subscribe(
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

  public resetSearch(): void {
    this.searchForm.reset({ searchText: STRING_EMPTY });
    this.search();
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
      .openForResult(MdIngredientModalComponent, {
        title: (data ? UPDATE_MODAL_TITLE : ADD_MODAL_TITLE) + ' nguyên liệu',
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
    this.masterDataService.delete(CONTROLLER, id).subscribe(
      (result) => {
        if (result.responseCode === 200) {
          notificationCommon(this.notifyService, SUCCESS, result.responseMessage);
          if (this.listData.length === 1 && this.table.page > 1) {
            this.table.page--;
          }
          this.getData();
        } else {
          notificationCommon(this.notifyService, ERROR, result.responseMessage);
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
}
