import { Renderer2 } from '@angular/core';
import { Router } from '@angular/router';
import moment from 'moment';
import { NotifyService } from '../shared/notify.service';
import {
  BMP,
  COMMA,
  DATE_FORMAT_YYYYMMDD,
  DOC,
  DOCX,
  DOMAIN_ROUTER,
  DOT,
  ENC,
  ERROR,
  GIF,
  HOME_ROUTE,
  INTERNAL_SERVER_ERROR,
  JPEG,
  JPG,
  NOTIFICATION_TITLE,
  PDF,
  PNG,
  PPT,
  PPTX,
  QUESTION_MARK,
  RAR,
  SOURCE,
  STRING_EMPTY,
  TAR,
  XLS,
  XLSX,
  Z7Z,
  ZIP,
} from './constant-helper';

export function checkClassActive(
  router: Router,
  renderer: Renderer2,
  listMenu?: any[]
): void {
  const url = router.url;
  if (
    !url.startsWith('/dang-nhap') &&
    url.split(QUESTION_MARK)[0] !== SOURCE.concat(DOMAIN_ROUTER, SOURCE, HOME_ROUTE) &&
    !checkPermission(url, listMenu)
  ) {
    router?.navigate(['/khong-co-quyen-truy-cap']);
    return;
  }
  // Active menu state is handled by the sidebar (main/share/sidebar)
  if (url.startsWith('/dang-nhap')) {
    renderer.addClass(document.body, 'body-login');
  } else {
    renderer.removeClass(document.body, 'body-login');
  }
}

export function notificationCommon(
  notifyService: NotifyService,
  type: string,
  message?: string
): void {
  notifyService.show(type, message);
}

export const checkValidateBetweenTwoDate = (startDate: any, endDate: any) => {
  if (
    startDate &&
    endDate &&
    new Date(moment(startDate).format(DATE_FORMAT_YYYYMMDD)) >
      new Date(moment(endDate).format(DATE_FORMAT_YYYYMMDD))
  ) {
    return true;
  }
  return false;
};

export const checkValidateGreaterTwoDate = (startDate: any, endDate: any) => {
  if (
    startDate &&
    endDate &&
    new Date(moment(startDate).format(DATE_FORMAT_YYYYMMDD)) >=
      new Date(moment(endDate).format(DATE_FORMAT_YYYYMMDD))
  ) {
    return true;
  }
  return false;
};

export const convertUrlToList = (url: any) => {
  return url.split(SOURCE);
};

export const listFileType = () => {
  return DOT.concat(
    DOC,
    COMMA,
    DOT,
    DOCX,
    COMMA,
    DOT,
    XLS,
    COMMA,
    DOT,
    XLSX,
    COMMA,
    DOT,
    BMP,
    COMMA,
    DOT,
    GIF,
    COMMA,
    DOT,
    JPEG,
    COMMA,
    DOT,
    JPG,
    COMMA,
    DOT,
    PNG,
    COMMA,
    DOT,
    Z7Z,
    COMMA,
    DOT,
    RAR,
    COMMA,
    DOT,
    TAR,
    COMMA,
    DOT,
    ENC,
    COMMA,
    DOT,
    ZIP,
    COMMA,
    DOT,
    PDF,
    COMMA,
    DOT,
    PPT,
    COMMA,
    DOT,
    PPTX
  );
};

export const randomCode = (stringLength: number) => {
  //const chars = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXTZabcdefghiklmnopqrstuvwxyz';
  const chars = '0123456789';
  let randomstring = STRING_EMPTY;
  for (let i = 0; i < stringLength; i++) {
    const rnum = Math.floor(Math.random() * chars.length);
    randomstring += chars.substring(rnum, rnum + 1);
  }
  return randomstring;
};

// ===== ObjectMenu.MetaLink <-> route (the ONE place that knows the MetaLink format) =====
// MetaLink is '#/<screen-id>' (D-35), '#/wf-inbox?type=<screen-id>' (approval items) or '#/group/NN'
// (level-1 groups). Older rows may still hold '<path>' or '/<path>'. Routes live under '/<DOMAIN_ROUTER>/'.
export const menuPath = (metaLink?: string | null): string =>
  (metaLink ?? STRING_EMPTY).trim().replace(/^#/, STRING_EMPTY).replace(/^\/+/, STRING_EMPTY);

export const menuUrl = (metaLink?: string | null): string =>
  SOURCE.concat(DOMAIN_ROUTER, SOURCE, menuPath(metaLink));

// routerLink + queryParams for a menu item (routerLink does not accept '?a=b' inside the path)
export const menuLink = (metaLink?: string | null): { path: string; queryParams: any } => {
  const [path, query] = menuUrl(metaLink).split(QUESTION_MARK);
  const queryParams: any = {};
  new URLSearchParams(query ?? STRING_EMPTY).forEach((v, k) => (queryParams[k] = v));
  return { path, queryParams };
};

export const isGroupMenu = (menu: any): boolean => menuPath(menu?.metaLink).startsWith('group/');

// Screen items of the 2-level listMenu: level-1 items without children (not groups) + every level-2 item
export const flattenMenu = (listMenu?: any[]): any[] =>
  (listMenu ?? []).flatMap((x) =>
    x.children?.length ? x.children : isGroupMenu(x) ? [] : [x]
  );

// Menu item of a router url: exact match (incl. ?type=…), else same path when the MetaLink has no query
export const findMenuByUrl = (url: string, listMenu?: any[]): any => {
  const items = flattenMenu(listMenu);
  const path = url.split(QUESTION_MARK)[0];
  return (
    items.find((x) => menuUrl(x.metaLink) === url) ??
    items.find((x) => !menuPath(x.metaLink).includes(QUESTION_MARK) && menuUrl(x.metaLink) === path)
  );
};

export function checkPermission(url: string, listMenu?: any[]): boolean {
  return !!findMenuByUrl(url, listMenu);
}

export const permission = (url: string, listMenu?: any[]) => {
  const per = findMenuByUrl(url, listMenu);
  if (!per) {
    return undefined;
  }
  return {
    allowAccess: per.allowAccess,
    allowAdd: per.allowAdd,
    allowEdit: per.allowEdit,
    allowDelete: per.allowDelete,
    allowPrint: per.allowPrint,
    allowExport: per.allowExport,
    allowImport: per.allowImport,
  };
};

import b64toBlob from 'b64-to-blob';
import { saveAs } from 'file-saver';
export const downloadFile = (
  fileContent: any,
  contentType: any,
  fileName: any
) => {
  const blob = b64toBlob(fileContent, contentType);
  saveAs(blob, fileName);
};
