import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NgbModalModule } from '@ng-bootstrap/ng-bootstrap';
import { DetailDrawerComponent } from './detail-drawer/detail-drawer.component';
import { DatePickerComponent } from './form/date-picker.component';
import { NumberInputDirective } from './form/number-input.directive';
import { SelectComponent } from './form/select.component';
import { PageHeaderComponent } from './page-header/page-header.component';
import { StatusBadgeComponent } from './status-badge/status-badge.component';
import { TablePagerComponent } from './table/table-pager.component';

// Everything a DELI-CARE screen needs on top of the template theme (Bootstrap 5, ng-bootstrap, select2)
// plus the D-36 shared parts: page header, status badge, detail drawer.
@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbModalModule,
    SelectComponent,
    DatePickerComponent,
    NumberInputDirective,
    TablePagerComponent,
    PageHeaderComponent,
    StatusBadgeComponent,
    DetailDrawerComponent,
  ],
  exports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgbModalModule,
    SelectComponent,
    DatePickerComponent,
    NumberInputDirective,
    TablePagerComponent,
    PageHeaderComponent,
    StatusBadgeComponent,
    DetailDrawerComponent,
  ],
})
export class SharedUiModule {}
