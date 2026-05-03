Feature: Household Setup

  Scenario: User creates a household
    Given I am logged in as "owner"
    When I navigate to "/household/create"
    And I fill in the household name "Smith Family"
    And I submit the household form
    Then I am redirected to "/"
