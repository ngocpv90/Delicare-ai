import { Component, Input, OnInit } from '@angular/core';
import {
  DOMAIN_ROUTER,
  HOME_ROUTE,
  SOURCE,
  STRING_EMPTY,
} from '../helpers/constant-helper';

@Component({
  selector: 'app-not-found',
  templateUrl: './not-found.component.html',
})
export class NotFoundComponent implements OnInit {
  @Input() public isFullPage = STRING_EMPTY;
  public domainRouter = SOURCE.concat(DOMAIN_ROUTER, SOURCE, HOME_ROUTE);
  public checkFullpage = false;

  public ngOnInit(): void {
    this.checkFullpage = this.isFullPage !== 'false';
  }
}
