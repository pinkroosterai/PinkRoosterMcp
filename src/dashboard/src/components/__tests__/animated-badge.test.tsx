import { render, fireEvent } from "@testing-library/react";
import { AnimatedBadge } from "../animated-badge";

describe("AnimatedBadge", () => {
  it("renders the badge with children", () => {
    const { getByText } = render(
      <AnimatedBadge value="NotStarted">NotStarted</AnimatedBadge>,
    );
    expect(getByText("NotStarted")).toBeInTheDocument();
  });

  it("does not apply pulse class on initial render", () => {
    const { getByText } = render(
      <AnimatedBadge value="NotStarted">NotStarted</AnimatedBadge>,
    );
    expect(getByText("NotStarted").closest("[class]")).not.toHaveClass(
      "animate-badge-pulse",
    );
  });

  it("applies pulse class when value changes", () => {
    const { getByText, rerender } = render(
      <AnimatedBadge value="NotStarted">NotStarted</AnimatedBadge>,
    );

    rerender(
      <AnimatedBadge value="Implementing">Implementing</AnimatedBadge>,
    );

    const badge = getByText("Implementing").closest("[class*='animate']") ??
      getByText("Implementing");
    expect(badge).toHaveClass("animate-badge-pulse");
  });

  it("removes pulse class after animation ends", async () => {
    const { getByText, rerender } = render(
      <AnimatedBadge value="NotStarted">NotStarted</AnimatedBadge>,
    );

    rerender(
      <AnimatedBadge value="Implementing">Implementing</AnimatedBadge>,
    );

    // The badge element itself has the onAnimationEnd handler
    const badgeEl = getByText("Implementing").closest("div") ?? getByText("Implementing");
    fireEvent.animationEnd(badgeEl);

    // After animationEnd, React re-renders without the class
    rerender(
      <AnimatedBadge value="Implementing">Implementing</AnimatedBadge>,
    );

    const updated = getByText("Implementing").closest("div") ?? getByText("Implementing");
    expect(updated).not.toHaveClass("animate-badge-pulse");
  });

  it("does not animate when value stays the same", () => {
    const { getByText, rerender } = render(
      <AnimatedBadge value="Implementing">Implementing</AnimatedBadge>,
    );

    rerender(
      <AnimatedBadge value="Implementing">Implementing</AnimatedBadge>,
    );

    const badge = getByText("Implementing");
    expect(badge.closest("[class]")).not.toHaveClass("animate-badge-pulse");
  });

  it("applies custom glow color via CSS variable", () => {
    const { container } = render(
      <AnimatedBadge value="NotStarted" glowColor="hsl(120 80% 50%)">
        NotStarted
      </AnimatedBadge>,
    );

    const badge = container.querySelector("[style]");
    expect(badge).toHaveStyle({ "--glow-color": "hsl(120 80% 50%)" });
  });
});
