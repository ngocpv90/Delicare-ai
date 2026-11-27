import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, of, throwError } from 'rxjs';
import { AuthenticationService, inStaffArea } from '../services/authentication.service';
import { URL_API } from './constant-helper';

@Injectable()
export class JwtInterceptor implements HttpInterceptor {
  constructor(
    private authenticationService: AuthenticationService,
    private router: Router
  ) {}

  private handleAuthError(err: HttpErrorResponse): Observable<any> {
    // 401 = not signed in / token expired (no refresh token yet, GAP-AUTH-01): drop the session, back to sign-in
    if (err.status === 401) {
      const returnUrl = this.router.url;
      this.authenticationService.clearSession();
      // Trang khách lẻ công khai gọi API lỗi 401 → chỉ xóa phiên, khách ở lại trang.
      if (inStaffArea() && !returnUrl.startsWith('/dang-nhap')) {
        this.router.navigate(['/dang-nhap'], { queryParams: { returnUrl } });
      }
      return of(err.message);
    }
    // 403 and everything else: the caller shows its own error (buttons are hidden by listMenu flags)
    return throwError(() => err);
  }

  intercept(
    request: HttpRequest<any>,
    next: HttpHandler
  ): Observable<HttpEvent<any>> {
    // add auth header with jwt if user is logged in and request is to the api url
    const token = this.authenticationService.userValue?.token;
    if (token && request.url.startsWith(URL_API)) {
      request = request.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`,
        },
      });
    }
    return next
      .handle(request)
      .pipe(catchError((x) => this.handleAuthError(x)));
  }
}
