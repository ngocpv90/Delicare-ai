import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HOME_ROUTE } from '../helpers/constant-helper';
import { MainComponent } from './main.component';
import { ComingSoonComponent } from './share/coming-soon/coming-soon.component';

// Route path = mockup screen-id = ObjectMenu.MetaLink without '#/' (D-35, CLAUDE.md 4.4).
// Old Vietnamese paths of the reused screens redirect to their screen-id.
// Hospital screens (patient*, medical-*, test-demo, delete-data) and the hospital dashboard are no longer
// routed (files kept). Menu items whose screen is not built yet fall through to ComingSoonComponent.
export const mainRoutes: Routes = [
  { path: '', redirectTo: HOME_ROUTE, pathMatch: 'full' },
  {
    path: '',
    component: MainComponent,
    children: [
      { path: HOME_ROUTE, component: ComingSoonComponent, data: { home: true } },
      // ===== P0 – Quản trị =====
      {
        path: 'adm-user',
        loadChildren: () =>
          import('./user/user.module').then((m) => m.UserModule),
      },
      {
        path: 'adm-usergroup',
        loadChildren: () =>
          import('./user-group/user-group.module').then(
            (m) => m.UserGroupModule
          ),
      },
      {
        path: 'adm-permission',
        loadChildren: () =>
          import('./user-permission/user-permission.module').then(
            (m) => m.UserPermissionModule
          ),
      },
      {
        path: 'adm-orgunit',
        loadChildren: () =>
          import('./organization-unit/organization-unit.module').then(
            (m) => m.OrganizationUnitModule
          ),
      },
      {
        path: 'adm-employee',
        loadChildren: () =>
          import('./employee/employee.module').then((m) => m.EmployeeModule),
      },
      {
        path: 'adm-position',
        loadChildren: () =>
          import('./position/position.module').then((m) => m.PositionModule),
      },
      {
        path: 'adm-objectmenu',
        loadChildren: () =>
          import('./object-menu/object-menu.module').then(
            (m) => m.ObjectMenuModule
          ),
      },
      {
        path: 'adm-person',
        loadChildren: () =>
          import('./person/person.module').then((m) => m.PersonModule),
      },
      {
        path: 'adm-access-log',
        loadChildren: () =>
          import('./location-tracking/location-tracking.module').then(
            (m) => m.LocationTrackingModule
          ),
      },
      {
        path: 'adm-audit',
        loadChildren: () =>
          import('./audit-change/audit-change.module').then(
            (m) => m.AuditChangeModule
          ),
      },
      // ===== P1 – Danh mục =====
      {
        path: 'scm-supplier',
        loadChildren: () =>
          import('./supplier/supplier.module').then((m) => m.SupplierModule),
      },
      {
        path: 'scm-uom',
        loadChildren: () =>
          import('./unit-measure/unit-measure.module').then(
            (m) => m.UnitMeasureModule
          ),
      },
      // ===== Old screens kept on their old paths (not in the DELI-CARE menu) =====
      {
        path: 'quan-ly-vung',
        loadChildren: () =>
          import('./administrative-region/administrative-region.module').then(
            (m) => m.AdministrativeRegionModule
          ),
      },
      {
        path: 'quan-ly-don-vi',
        loadChildren: () =>
          import('./administrative-unit/administrative-unit.module').then(
            (m) => m.AdministrativeUnitModule
          ),
      },
      {
        path: 'quan-ly-tinh-thanh',
        loadChildren: () =>
          import(
            './administrative-province/administrative-province.module'
          ).then((m) => m.AdministrativeProvinceModule),
      },
      {
        path: 'quan-ly-quan-huyen',
        loadChildren: () =>
          import(
            './administrative-district/administrative-district.module'
          ).then((m) => m.AdministrativeDistrictModule),
      },
      {
        path: 'quan-ly-phuong-xa',
        loadChildren: () =>
          import('./administrative-ward/administrative-ward.module').then(
            (m) => m.AdministrativeWardModule
          ),
      },
      {
        path: 'ql-vung',
        loadChildren: () =>
          import('./orgchart/region/region.module').then(
            (m) => m.RegionModule
          ),
      },
      {
        path: 'ql-khu-vuc',
        loadChildren: () =>
          import('./orgchart/area/area.module').then(
            (m) => m.AreaModule
          ),
      },
      // ===== Old paths -> screen-id =====
      { path: 'quan-ly-tai-khoan', redirectTo: 'adm-user' },
      { path: 'phan-quyen-tai-khoan', redirectTo: 'adm-usergroup' },
      { path: 'quan-ly-chuc-nang', redirectTo: 'adm-objectmenu' },
      { path: 'nguoi-dung', redirectTo: 'adm-person' },
      { path: 'quan-ly-dang-nhap', redirectTo: 'adm-access-log' },
      { path: 'quan-ly-thay-doi-du-lieu', redirectTo: 'adm-audit' },
      { path: 'nha-cung-cap', redirectTo: 'scm-supplier' },
      { path: 'don-vi-tinh', redirectTo: 'scm-uom' },
      // Menu item without a built screen yet (incl. 'wf-inbox?type=…', groups): placeholder, no crash
      { path: '**', component: ComingSoonComponent },
    ],
  },
];
@NgModule({
  imports: [RouterModule.forChild(mainRoutes)],
  exports: [RouterModule],
})
export class MainRoutingModule {}
