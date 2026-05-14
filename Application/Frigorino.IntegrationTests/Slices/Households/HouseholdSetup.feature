Feature: Household Setup

  Scenario: User creates a household
    Given I am logged in as "owner"
    When I navigate to "/household/create"
    And I fill in the household name "Smith Family"
    And I submit the household form
    Then I am redirected to "/"

  Scenario: Owner deletes their household and is redirected to root
    Given I am logged in with an active household
    When I navigate to "/household/manage"
    And I open the household management menu
    And I select delete household from the menu
    And I type "Test Household" into the delete confirmation input
    And I confirm the household deletion
    Then I am redirected to "/"

  Scenario: Delete confirmation requires the exact household name
    Given I am logged in with an active household
    When I navigate to "/household/manage"
    And I open the household management menu
    And I select delete household from the menu
    Then the delete confirmation button is disabled
    When I type "Wrong Name" into the delete confirmation input
    Then the delete confirmation button is disabled
    When I type "Test Household" into the delete confirmation input
    Then the delete confirmation button is enabled

  Scenario: Cancel keeps the household intact
    Given I am logged in with an active household
    When I navigate to "/household/manage"
    And I open the household management menu
    And I select delete household from the menu
    And I cancel the household deletion
    Then the delete confirmation dialog is closed
    When I navigate to "/"
    Then the active household should be "Test Household"

  Scenario: Owner with two households deletes the active one and the other remains
    Given I am logged in as "owner"
    When I navigate to "/household/create"
    And I fill in the household name "Alpha"
    And I submit the household form
    Then I am redirected to "/"
    When I navigate to "/household/create"
    And I fill in the household name "Beta"
    And I submit the household form
    Then I am redirected to "/"
    When I switch the active household to "Alpha"
    And I navigate to "/household/manage"
    And I open the household management menu
    And I select delete household from the menu
    And I type "Alpha" into the delete confirmation input
    And I confirm the household deletion
    Then I am redirected to "/"
    And the active household should be "Beta"

  Scenario: Member cannot see the delete option on the manage page
    Given I am logged in as "member"
    And an existing household "Family" owned by "owner" with me as a "member"
    When I navigate to "/household/manage"
    Then the household management menu trigger is not visible

  Scenario: Empty household name returns a 400 validation error
    Given I am logged in as "owner"
    When I POST a household with an empty name via the API
    Then the API response status is 400
    And the API response has a validation error for "Name"
