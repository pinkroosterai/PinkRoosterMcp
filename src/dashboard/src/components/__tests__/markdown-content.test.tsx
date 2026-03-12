import { render, screen } from "@testing-library/react";
import { MarkdownContent } from "../markdown-content";

describe("MarkdownContent", () => {
  it("renders bold text as strong element", () => {
    render(<MarkdownContent content="This is **bold** text" />);
    const strong = document.querySelector(".prose strong");
    expect(strong).toBeInTheDocument();
    expect(strong?.textContent).toBe("bold");
  });

  it("renders italic text as em element", () => {
    render(<MarkdownContent content="This is *italic* text" />);
    const em = document.querySelector(".prose em");
    expect(em).toBeInTheDocument();
    expect(em?.textContent).toBe("italic");
  });

  it("renders headings", () => {
    render(<MarkdownContent content="## Heading Two" />);
    expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Heading Two");
  });

  it("renders links", () => {
    render(<MarkdownContent content="[Click here](https://example.com)" />);
    const link = screen.getByRole("link", { name: "Click here" });
    expect(link).toHaveAttribute("href", "https://example.com");
  });

  it("renders code blocks", () => {
    render(<MarkdownContent content={"`inline code`"} />);
    const code = document.querySelector(".prose code");
    expect(code).toBeInTheDocument();
    expect(code?.textContent).toBe("inline code");
  });

  it("renders unordered lists", () => {
    render(<MarkdownContent content={"- Item A\n- Item B"} />);
    const items = screen.getAllByRole("listitem");
    expect(items.length).toBeGreaterThanOrEqual(1);
  });

  it("returns null for null content", () => {
    const { container } = render(<MarkdownContent content={null} />);
    expect(container.innerHTML).toBe("");
  });

  it("returns null for undefined content", () => {
    const { container } = render(<MarkdownContent content={undefined} />);
    expect(container.innerHTML).toBe("");
  });

  it("returns null for empty string", () => {
    const { container } = render(<MarkdownContent content="" />);
    expect(container.innerHTML).toBe("");
  });

  it("applies custom className", () => {
    render(<MarkdownContent content="Hello" className="custom-class" />);
    const wrapper = document.querySelector(".prose.custom-class");
    expect(wrapper).toBeInTheDocument();
  });

  it("renders GFM strikethrough", () => {
    render(<MarkdownContent content="~~strikethrough~~" />);
    const del = document.querySelector(".prose del");
    expect(del).toBeInTheDocument();
    expect(del?.textContent).toBe("strikethrough");
  });
});
