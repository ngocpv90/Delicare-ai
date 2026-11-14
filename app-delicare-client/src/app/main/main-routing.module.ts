import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MainComponent } from './main.component';

export const mainRoutes: Routes = [
  { path: '', redirectTo: 'trang-chu', pathMatch: 'full' },
  {
    path: '',
    component: MainComponent,
    children: [
      {
        path: 'trang-chu',
        loadChildren: () =>
          import('./dashboard/dashboard.module').then((m) => m.DashboardModule),
      },
      {
        path: 'quan-ly-tai-khoan',
        loadChildren: () =>
          import('./user/user.module').then((m) => m.UserModule),
      },
      {
        path: 'phan-quyen-tai-khoan',
        loadChildren: () =>
          import('./user-group/user-group.module').then(
            (m) => m.UserGroupModule
          ),
      },
      {
        path: 'quan-ly-chuc-nang',
        loadChildren: () =>
          import('./object-menu/object-menu.module').then(
            (m) => m.ObjectMenuModule
          ),
      },
      {
        path: 'quan-ly-dang-nhap',
        loadChildren: () =>
          import('./location-tracking/location-tracking.module').then(
            (m) => m.LocationTrackingModule
          ),
      },
      {
        path: 'quan-ly-thay-doi-du-lieu',
        loadChildren: () =>
          import('./audit-change/audit-change.module').then(
            (m) => m.AuditChangeModule
          ),
      },
      {
        path: 'quan-ly-xoa-du-lieu-cu',
        loadChildren: () =>
          import('./delete-data/delete-data.module').then(
            (m) => m.DeleteDataModule
          ),
      },
      {
        path: 'chuc-nang-demo',
        loadChildren: () =>
          import('./test-demo/test-demo.module').then((m) => m.TestDemoModule),
      },
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
        path: 'nguoi-dung',
        loadChildren: () =>
          import('./person/person.module').then((m) => m.PersonModule),
      },
      {
        path: 'kham-benh',
        loadChildren: () =>
          import('./medical-examination/medical-examination.module').then(
            (m) => m.MedicalExaminationModule
          ),
      },
      {
        path: 'kho-thuoc-le',
        loadChildren: () =>
          import('./medical-warehouse-odd/medical-warehouse-odd.module').then(
            (m) => m.MedicalWarehouseOddModule
          ),
      },
      {
        path: 'kho-thuoc-chinh',
        loadChildren: () =>
          import('./medical-warehouse-main/medical-warehouse-main.module').then(
            (m) => m.MedicalWarehouseMainModule
          ),
      },
      // <master-data-routes>
      {
        path: 'dm-cong-ty',
        loadChildren: () =>
          import('./master-data/company/md-company.module').then(
            (m) => m.MdCompanyModule
          ),
      },
      {
        path: 'dm-don-vi-to-chuc',
        loadChildren: () =>
          import('./master-data/organization-unit/md-organization-unit.module').then(
            (m) => m.MdOrganizationUnitModule
          ),
      },
      {
        path: 'dm-dia-diem-van-hanh',
        loadChildren: () =>
          import('./master-data/operational-location/md-operational-location.module').then(
            (m) => m.MdOperationalLocationModule
          ),
      },
      {
        path: 'dm-khach-hang',
        loadChildren: () =>
          import('./master-data/customer/md-customer.module').then(
            (m) => m.MdCustomerModule
          ),
      },
      {
        path: 'dm-lien-he-khach-hang',
        loadChildren: () =>
          import('./master-data/customer-contact/md-customer-contact.module').then(
            (m) => m.MdCustomerContactModule
          ),
      },
      {
        path: 'dm-dia-diem-giao-hang',
        loadChildren: () =>
          import('./master-data/customer-delivery-location/md-customer-delivery-location.module').then(
            (m) => m.MdCustomerDeliveryLocationModule
          ),
      },
      {
        path: 'nha-cung-cap',
        loadChildren: () =>
          import('./master-data/supplier/md-supplier.module').then(
            (m) => m.MdSupplierModule
          ),
      },
      {
        path: 'dm-nguyen-lieu-ncc',
        loadChildren: () =>
          import('./master-data/supplier-material/md-supplier-material.module').then(
            (m) => m.MdSupplierMaterialModule
          ),
      },
      {
        path: 'dm-nguyen-lieu',
        loadChildren: () =>
          import('./master-data/ingredient/md-ingredient.module').then(
            (m) => m.MdIngredientModule
          ),
      },
      {
        path: 'dm-mon-an',
        loadChildren: () =>
          import('./master-data/dish/md-dish.module').then(
            (m) => m.MdDishModule
          ),
      },
      {
        path: 'dm-kho',
        loadChildren: () =>
          import('./master-data/warehouse/md-warehouse.module').then(
            (m) => m.MdWarehouseModule
          ),
      },
      {
        path: 'dm-khu-vuc-kho',
        loadChildren: () =>
          import('./master-data/storage-zone/md-storage-zone.module').then(
            (m) => m.MdStorageZoneModule
          ),
      },
      {
        path: 'dm-vi-tri-luu-tru',
        loadChildren: () =>
          import('./master-data/storage-location/md-storage-location.module').then(
            (m) => m.MdStorageLocationModule
          ),
      },
      {
        path: 'dm-nhan-vien',
        loadChildren: () =>
          import('./master-data/employee/md-employee.module').then(
            (m) => m.MdEmployeeModule
          ),
      },
      {
        path: 'dm-phuong-tien',
        loadChildren: () =>
          import('./master-data/vehicle/md-vehicle.module').then(
            (m) => m.MdVehicleModule
          ),
      },
      {
        path: 'dm-nhom-nguyen-lieu',
        loadChildren: () =>
          import('./master-data/ingredient-category/md-ingredient-category.module').then(
            (m) => m.MdIngredientCategoryModule
          ),
      },
      {
        path: 'dm-don-vi-tinh',
        loadChildren: () =>
          import('./master-data/unit-of-measure/md-unit-of-measure.module').then(
            (m) => m.MdUnitOfMeasureModule
          ),
      },
      {
        path: 'dm-nhom-mon-an',
        loadChildren: () =>
          import('./master-data/dish-category/md-dish-category.module').then(
            (m) => m.MdDishCategoryModule
          ),
      },
      {
        path: 'dm-chuc-vu',
        loadChildren: () =>
          import('./master-data/position/md-position.module').then(
            (m) => m.MdPositionModule
          ),
      },
      // </master-data-routes>
      {
        path: 'don-vi-tinh',
        loadChildren: () =>
          import('./unit-measure/unit-measure.module').then(
            (m) => m.UnitMeasureModule
          ),
      },
      {
        path: 'thuoc-sap-het-han',
        loadChildren: () =>
          import(
            './medical-warehouse-expiry/medical-warehouse-expiry.module'
          ).then((m) => m.MedicalWarehouseExpiryModule),
      },
      {
        path: 'benh-nhan',
        loadChildren: () =>
          import('./patient/patient.module').then((m) => m.PatientModule),
      },
      {
        path: 'nhap-ket-qua',
        loadChildren: () =>
          import('./patient-result/patient-result.module').then(
            (m) => m.PatientResultModule
          ),
      },
      {
        path: 'danh-muc-khach-hang',
        loadChildren: () =>
          import('./patient-result/patient-result.module').then(
            (m) => m.PatientResultModule
          ),
      },
      {
        path: 'khach-hang-360',
        loadChildren: () =>
          import('./patient-result/patient-result.module').then(
            (m) => m.PatientResultModule
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
      {
        path: 'ql-chi-nhanh',
        loadChildren: () =>
          import('./patient-result/patient-result.module').then(
            (m) => m.PatientResultModule
          ),
      },
      {
        path: 'ql-phong-ban',
        loadChildren: () =>
          import('./patient-result/patient-result.module').then(
            (m) => m.PatientResultModule
          ),
      },
      {
        path: 'ql-san-pham',
        loadChildren: () =>
          import('./patient-result/patient-result.module').then(
            (m) => m.PatientResultModule
          ),
      },
      {
        path: 'ql-nhu-cau-khach-hang',
        loadChildren: () =>
          import('./patient-result/patient-result.module').then(
            (m) => m.PatientResultModule
          ),
      },
      {
        path: 'ql-co-hoi-ban-hang',
        loadChildren: () =>
          import('./patient-result/patient-result.module').then(
            (m) => m.PatientResultModule
          ),
      },
    ],
  },
];
@NgModule({
  imports: [RouterModule.forChild(mainRoutes)],
  exports: [RouterModule],
})
export class MainRoutingModule {}
