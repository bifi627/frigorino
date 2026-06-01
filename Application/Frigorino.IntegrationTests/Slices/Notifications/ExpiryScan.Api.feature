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
