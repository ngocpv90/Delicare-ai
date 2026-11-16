import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, of } from 'rxjs';
import Swal from 'sweetalert2';
import { catchError, map } from 'rxjs/operators';
import {
  AUTHEN,
  NOTIFICATION_TITLE,
  UN_AUTHORIZED,
  URL_API,
} from '../helpers/constant-helper';
import { User } from '../helpers/user';

@Injectable({
  providedIn: 'root',
})
export class AuthenticationService {
  private userSubject: BehaviorSubject<User | null>;
  public user: Observable<User | null>;
  // api/User/GetCurrent is called once per page load (AuthGuard) to refresh user + menu after a reload
  private currentLoaded = false;
  private autoLogoutTimer: any;

  constructor(
    private http: HttpClient,
    private router: Router
  ) {
    this.userSubject = new BehaviorSubject<User | null>(this.readStoredUser());
    this.user = this.userSubject.asObservable();
  }

  public get userValue(): User | null {
    return this.userSubject.value;
  }

  private readStoredUser(): User | null {
    try {
      const user = JSON.parse(localStorage.getItem(AUTHEN) || 'null');
      return user?.token ? user : null;
    } catch {
      return null;
    }
  }

  public signIn(userName: string, passWord: string): any {
    return this.http
      .post<any>(`${URL_API}Authentication/Login`, { userName, passWord })
      .pipe(
        map((user) => {
          // store user details and basic auth credentials in local storage to keep user logged in between page refreshes
          if (user.data) {
            const date = new Date().getTime();
            const { expireTime } = user.data;
            const exp = date + expireTime * 1000;
            const data = { ...user.data, expireTime: exp };
            localStorage.setItem(AUTHEN, JSON.stringify(data));
            this.userSubject.next(data);
            // Login already returned the menu: no GetCurrent needed until the next reload
            this.currentLoaded = true;
            this.autoLogout(user.data.expireTime * 1000);
            return user;
          }
          return user;
        })
      );
  }

  // Reload: refresh user info + listMenu from api/User/GetCurrent ({ user, listMenu }), keep the stored token.
  // Never fails: a 401 is handled by JwtInterceptor (session cleared, back to the sign-in page).
  public loadCurrent(): Observable<boolean> {
    if (this.currentLoaded || !this.userValue?.token) {
      return of(!!this.userValue?.token);
    }
    return this.http.get<any>(`${URL_API}User/GetCurrent`).pipe(
      map((res) => {
        if (res?.responseCode === 200 && res.data && this.userValue) {
          const u = res.data.user ?? {};
          const data: User = {
            ...this.userValue,
            fullName: u.fullName ?? this.userValue.fullName,
            userName: u.userName ?? this.userValue.userName,
            avatar: u.avatar ?? this.userValue.avatar,
            isAdmin: u.isAdmin ?? this.userValue.isAdmin,
            listMenu: res.data.listMenu ?? [],
          };
          localStorage.setItem(AUTHEN, JSON.stringify(data));
          this.userSubject.next(data);
          this.currentLoaded = true;
        }
        return !!this.userValue?.token;
      }),
      catchError(() => of(!!this.userValue?.token))
    );
  }

  // Drop the session without navigating (used by logout and by JwtInterceptor on 401)
  public clearSession(): void {
    localStorage.removeItem(AUTHEN);
    this.currentLoaded = false;
    clearTimeout(this.autoLogoutTimer);
    this.userSubject.next(null);
  }

  public logout(): void {
    this.clearSession();
    this.router.navigate(['/dang-nhap']);
  }

  private autoLogout(expireDate: number): any {
    clearTimeout(this.autoLogoutTimer);
    this.autoLogoutTimer = setTimeout(() => {
      Swal.fire({
        icon: 'warning',
        title: NOTIFICATION_TITLE,
        text: UN_AUTHORIZED,
        confirmButtonText: 'Đồng ý',
        confirmButtonColor: 'var(--primary)',
        allowOutsideClick: false,
        allowEscapeKey: false,
      }).then(() => this.logout());
    }, expireDate);
  }

  public autoLogin(): void {
    const userData = this.readStoredUser();
    if (!userData?.userName) {
      // Not signed in: nothing to restore. Guarded routes send the user to the sign-in page.
      this.clearSession();
      return;
    }
    const todaysDate = new Date().getTime();
    const expireDate = userData.expireTime;
    if (todaysDate > expireDate) {
      this.logout();
    } else {
      this.autoLogout(expireDate - todaysDate);
    }
  }
}
