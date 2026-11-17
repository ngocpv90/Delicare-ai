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
import { AuthenticationService } from '../services/authentication.service';
import { URL_API } from './constant-helper';

@Injectable()
export class JwtInterceptor implements HttpInterceptor {
  constructor(
    private authenticationService: AuthenticationService,
    private router: Router
  ) {}

  private handleAuthError(err: HttpErrorResponse): Observable<any> {
    //handle your auth error or rethrow
    // 401 = hết phiên → về trang đăng nhập. 403 = thiếu quyền trên một màn hình: KHÔNG đăng xuất,
    // trả lỗi cho nơi gọi tự báo (giao diện DELICARE đọc nhiều bảng cùng lúc, thiếu quyền một bảng là chuyện bình thường).
    if (err.status === 401) {
      this.router.navigate(['/dang-nhap']);
      return of(err.message);
    }
    return throwError(() => err);
  }

  intercept(
    request: HttpRequest<any>,
    next: HttpHandler
  ): Observable<HttpEvent<any>> {
    // add auth header with jwt if user is logged in and request is to the api url
    const user = this.authenticationService.userValue;
    if (user !== null) {
      const isLoggedIn = user && user.token;
      const isApiUrl = request.url.startsWith(URL_API);
      if (isLoggedIn && isApiUrl) {
        request = request.clone({
          setHeaders: {
            Authorization: `Bearer ${user.token}`,
          },
        });
      }
      return next
        .handle(request)
        .pipe(catchError((x) => this.handleAuthError(x)));
    }
    return next
      .handle(request)
      .pipe(catchError((x) => this.handleAuthError(x)));
  }
}
