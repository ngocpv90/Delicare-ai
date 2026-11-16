import { Component, Input, OnInit } from '@angular/core';
import {
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
} from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { toFormData } from 'src/app/helpers/common-helper';
import { COMMA, STRING_EMPTY } from 'src/app/helpers/constant-helper';
import { AuthenticationService } from 'src/app/services/authentication.service';
import { PersonService } from 'src/app/services/person.service';
import { UserGroupService } from 'src/app/services/user-group.service';
import { UserService } from 'src/app/services/user.service';
import { NotifyService } from 'src/app/shared/notify.service';

// adm-user create / edit. Sends every UserRequest field (Update overwrites): Id, UserName, PersonId,
// IsActive, IsAdmin, IsDefault, ListUserGroupId (comma separated group ids).
@Component({
  selector: 'app-user-modal',
  templateUrl: './user-modal.component.html',
})
export class UserModalComponent implements OnInit {
  public isLoading = false;
  public isLoadingUserGroupId = false;
  public formGroup!: UntypedFormGroup;
  @Input() public title = STRING_EMPTY;
  @Input() public data: any;
  public submitted = false;
  public listUserGroup: any[] = [];
  public isReadonly = false;
  public isLoadingPerson = false;
  public listPerson: any[] = [];
  // Only an admin may grant / remove the admin flag (the flag is still sent unchanged by others)
  public isAdmin = false;

  constructor(
    private formBuilder: UntypedFormBuilder,
    private userGroupService: UserGroupService,
    private userService: UserService,
    private personService: PersonService,
    private authenticationService: AuthenticationService,
    private notify: NotifyService,
    private activeModal: NgbActiveModal
  ) {}

  public ngOnInit(): void {
    this.isAdmin = !!this.authenticationService.userValue?.isAdmin;
    this.getAllDataUserGroup();
    this.formGroup = this.formBuilder.group({
      id: [STRING_EMPTY],
      userName: [STRING_EMPTY, [Validators.required, Validators.pattern(/^\S+$/)]],
      personId: [null],
      listUserGroupId: [[], [Validators.required]],
      isActive: [true],
      isAdmin: [false],
      isDefault: [false],
    });
    if (this.data) {
      this.isLoading = true;
      this.userService.getById(this.data.id).subscribe(
        (data) => {
          if (data.responseCode === 200) {
            const item = data.data;
            item.listUserGroupId = item.listUserGroupId ? item.listUserGroupId.split(COMMA) : [];
            // Keep the current person selectable before any search
            if (item.personId) {
              this.listPerson = [{ id: item.personId, fullName: item.fullName ?? STRING_EMPTY }];
            }
            this.formGroup.patchValue(item);
            this.isReadonly = true;
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

  private getAllDataUserGroup(): void {
    this.isLoadingUserGroupId = true;
    this.userGroupService.getAllData().subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.listUserGroup = data.listData;
        }
      },
      () => {
        this.isLoadingUserGroupId = false;
        this.notify.error();
      },
      () => {
        this.isLoadingUserGroupId = false;
      }
    );
  }

  public destroyModal(reLoad: boolean): void {
    this.activeModal.close({ reLoad });
  }

  public isInvalid(name: string): boolean {
    const control = this.formGroup.controls[name];
    return control.invalid && (this.submitted || control.dirty || control.touched);
  }

  // Person/GetAllDataByKey: ≥ 3 characters; new account = only people without an active account
  public personSearch(value: string): void {
    if (!value || value.trim().length < 3) {
      return;
    }
    this.isLoadingPerson = true;
    const current = this.listPerson.find((x) => x.id === this.formGroup.value.personId);
    this.personService.getAllDataByKey(value.trim(), this.isReadonly).subscribe(
      (data) => {
        if (data.responseCode === 200) {
          const list: any[] = data.listData ?? [];
          this.listPerson = current && !list.some((x) => x.id === current.id) ? [current, ...list] : list;
        }
      },
      () => {
        this.isLoadingPerson = false;
        this.notify.error();
      },
      () => {
        this.isLoadingPerson = false;
      }
    );
  }

  public submitForm(): void {
    this.submitted = true;
    if (!this.formGroup.valid) {
      return;
    }
    this.isLoading = true;
    const value = this.formGroup.value;
    const params = {
      id: value.id || STRING_EMPTY,
      userName: (value.userName ?? STRING_EMPTY).trim(),
      personId: value.personId ?? STRING_EMPTY,
      isActive: !!value.isActive,
      isAdmin: !!value.isAdmin,
      isDefault: !!value.isDefault,
      listUserGroupId: (value.listUserGroupId ?? []).join(COMMA),
    };
    const formData = toFormData(params);
    const request = params.id ? this.userService.update(formData) : this.userService.insert(formData);
    request.subscribe(
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
