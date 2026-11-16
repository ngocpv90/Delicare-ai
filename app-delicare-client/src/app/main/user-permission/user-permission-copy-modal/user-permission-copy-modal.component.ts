import { Component, Input, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { toFormData } from 'src/app/helpers/common-helper';
import { STRING_EMPTY } from 'src/app/helpers/constant-helper';
import { UserRoleService } from 'src/app/services/user-role.service';
import { NotifyService } from 'src/app/shared/notify.service';

// "Sao chép từ nhóm…": UserRole/CopyFromUserGroup (form SourceUserGroupId, TargetUserGroupId).
// The target's permissions become exactly the source's (menus the source lacks are revoked).
@Component({
  selector: 'app-user-permission-copy-modal',
  templateUrl: './user-permission-copy-modal.component.html',
})
export class UserPermissionCopyModalComponent implements OnInit {
  @Input() public title = STRING_EMPTY;
  // { target: group, listGroup: group[] }
  @Input() public data: any;
  public listSource: any[] = [];
  public sourceId: string | null = null;
  public submitted = false;
  public isLoading = false;

  constructor(
    private userRoleService: UserRoleService,
    private notify: NotifyService,
    private activeModal: NgbActiveModal
  ) {}

  public ngOnInit(): void {
    this.listSource = (this.data?.listGroup ?? []).filter((x: any) => x.id !== this.data?.target?.id);
  }

  public destroyModal(reLoad: boolean): void {
    this.activeModal.close({ reLoad });
  }

  public submitForm(): void {
    this.submitted = true;
    if (!this.sourceId) {
      return;
    }
    this.isLoading = true;
    this.userRoleService
      .copyFromUserGroup(
        toFormData({ sourceUserGroupId: this.sourceId, targetUserGroupId: this.data.target.id })
      )
      .subscribe(
        (data) => {
          if (data.responseCode === 200) {
            this.notify.success(data.responseMessage);
            this.destroyModal(true);
          } else {
            this.notify.error(data.responseMessage);
          }
        },
        () => {
          this.isLoading = false;
          this.notify.error();
        },
        () => {
          this.isLoading = false;
        }
      );
  }
}
