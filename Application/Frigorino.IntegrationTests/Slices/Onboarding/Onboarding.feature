Feature: First-run onboarding

  Scenario: A user with no households is sent to onboarding
    Given I am logged in as "newcomer"
    When I navigate to "/"
    Then I am redirected to "/onboarding"

  Scenario: Creating the first household from onboarding opens the dashboard
    Given I am logged in as "newcomer"
    When I navigate to "/onboarding"
    And I fill in the household name "My Home"
    And I submit the household form
    Then I am redirected to "/"
    And the active household should be "My Home"

  Scenario: Skipping onboarding enters the dashboard without bouncing back
    Given I am logged in as "newcomer"
    When I navigate to "/onboarding"
    And I skip onboarding
    Then I am redirected to "/"
    When I reload the page
    Then the onboarding page is not shown
