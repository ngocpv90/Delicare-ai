export interface User {
  userId: string;
  id: string;
  userName: string;
  fullName: string;
  avatar: string;
  userGroupName: string;
  isAdmin: boolean;
  token: string;
  expireTime: number;
  listMenu: any[];
  /** T45R.8 (D-39): đăng nhập bằng mật khẩu tạm – phải đổi mật khẩu trước khi dùng. */
  mustChangePassword?: boolean;
}
