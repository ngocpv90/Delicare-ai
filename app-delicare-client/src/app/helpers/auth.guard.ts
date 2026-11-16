import { Injectable } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { Observable, map } from 'rxjs';
import { AuthenticationService } from '../services/authentication.service';

@Injectable({
  providedIn: 'root',
})
export class AuthGuard {
  constructor(
    private router: Router,
    private authenticationService: AuthenticationService
  ) {}
  canActivate(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ):
    | Observable<boolean | UrlTree>
    | Promise<boolean | UrlTree>
    | boolean
    | UrlTree {
    const signIn = () =>
      this.router.createUrlTree(['/dang-nhap'], {
        queryParams: { returnUrl: state.url },
      });
    if (!this.authenticationService.userValue?.token) {
      return signIn();
    }
    // First guarded navigation after a reload: refresh user + menu (api/User/GetCurrent)
    return this.authenticationService
      .loadCurrent()
      .pipe(map((ok) => (ok ? true : signIn())));
  }
}
