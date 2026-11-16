import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { UserPermissionCopyModalComponent } from './user-permission-copy-modal/user-permission-copy-modal.component';
import { UserPermissionRoutingModule } from './user-permission-routing.module';
import { UserPermissionComponent } from './user-permission.component';

@NgModule({
  declarations: [UserPermissionComponent, UserPermissionCopyModalComponent],
  imports: [
    SharedUiModule,
    UserPermissionRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class UserPermissionModule {}
