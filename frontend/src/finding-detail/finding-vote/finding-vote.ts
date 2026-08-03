import { Component, computed, input, output } from '@angular/core';
import { BuryReason, FindingDetailDto } from '../finding-detail.service';
import { CURRENT_USER } from '../current-user';
import { MatButton } from '@angular/material/button';
import { MatMenu, MatMenuTrigger, MatMenuItem } from '@angular/material/menu';

@Component({
  selector: 'app-finding-vote',
  imports: [MatButton, MatMenu, MatMenuTrigger, MatMenuItem],
  templateUrl: './finding-vote.html',
  styleUrl: './finding-vote.scss',
})
export class FindingVote {
  readonly finding = input.required<FindingDetailDto>();
  readonly votePending = input<boolean>(false);
  readonly dig = output<void>();
  readonly bury = output<BuryReason>();

  protected readonly isOwnFinding = computed(() => this.finding().author === CURRENT_USER);
  protected readonly isDisabled = computed(() => this.votePending() || this.isOwnFinding());
}
