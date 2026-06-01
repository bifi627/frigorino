Feature: Expiry scan API

  Background:
    Given I am logged in with an active household

  Scenario: A scan dispatches one digest for an eligible recipient
    Given I am opted in to expiry notifications with a registered device
    And an inventory "Fridge" with an item "Milk" expiring in 1 day
    When I POST the expiry scan with a valid maintenance key
    Then the API response status is 200
    And exactly 1 notification dispatch exists for me today

  Scenario: Re-firing the scan does not create a duplicate dispatch
    Given I am opted in to expiry notifications with a registered device
    And an inventory "Fridge" with an item "Milk" expiring in 1 day
    When I POST the expiry scan with a valid maintenance key
    And I POST the expiry scan with a valid maintenance key
    Then the API response status is 200
    And exactly 1 notification dispatch exists for me today

  Scenario: A scan with a bad maintenance key is not discoverable
    When I POST the expiry scan with an invalid maintenance key
    Then the API response status is 404

  # Two scans fired at once exercise the real (UserId, HouseholdId, SentOn) unique index. They may
  # serialize (the in-memory pre-filter handles the second) OR genuinely race (the loser catches
  # SQLSTATE 23505) — either way the invariant is one dispatch row and no 500. This only goes red if
  # the claim-slot-first fix regresses (a duplicate row, or a DbUpdateException bubbling to a 500).
  Scenario: Concurrent scans still produce exactly one dispatch
    Given I am opted in to expiry notifications with a registered device
    And an inventory "Fridge" with an item "Milk" expiring in 1 day
    When I trigger the expiry scan twice concurrently
    Then both concurrent API responses have status 200
    And exactly 1 notification dispatch exists for me today
