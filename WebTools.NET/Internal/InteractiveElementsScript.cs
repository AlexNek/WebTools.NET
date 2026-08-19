namespace WebTools.NET.Internal;

/// <summary>
/// JavaScript executed in the browser to extract interactive elements from the live DOM.
/// Open shadow roots are traversed; selectors are generated deterministically and checked
/// for uniqueness within their DOM root.
/// </summary>
internal static class InteractiveElementsScript
{
    /// <summary>Returns visible, actionable elements and their executable CSS selectors.</summary>
    internal const string Script = """
        () => {
            const MAX_LABEL = 80;
            const results = [];
            const seen = new Set();

            function isVisible(el) {
                const ariaDisabled = (el.getAttribute('aria-disabled') || '').toLowerCase() === 'true';
                if (el.disabled || ariaDisabled || el.readOnly) return false;

                let current = el;
                while (current) {
                    if (current.disabled && current.tagName?.toLowerCase() === 'fieldset') return false;
                    if (current.getAttribute?.('aria-hidden') === 'true') return false;
                    current = current.parentElement || current.getRootNode?.().host || null;
                }

                const style = getComputedStyle(el);
                if (style.display === 'none' || style.visibility === 'hidden' || style.visibility === 'collapse') return false;
                const rect = el.getBoundingClientRect();
                if (rect.width === 0 || rect.height === 0) return false;

                return true;
            }

            function getLabel(el) {
                const candidates = [
                    el.innerText,
                    el.getAttribute('aria-label'),
                    el.getAttribute('placeholder'),
                    el.getAttribute('title'),
                    el.getAttribute('name')
                ];
                const label = candidates.find(value => value && value.trim()) || '';
                return label.trim().substring(0, MAX_LABEL);
            }

            function queryCount(root, selector) {
                try {
                    return root.querySelectorAll(selector).length;
                } catch {
                    return 0;
                }
            }

            function getLocalSelector(el, root) {
                const tag = el.tagName.toLowerCase();
                if (el.id) {
                    const idSelector = '#' + CSS.escape(el.id);
                    if (queryCount(root, idSelector) === 1) return idSelector;
                }

                if (el.name) {
                    const nameSelector = tag + '[name="' + CSS.escape(el.name) + '"]';
                    if (queryCount(root, nameSelector) === 1) return nameSelector;
                }

                const classes = Array.from(el.classList || [])
                    .filter(Boolean)
                    .map(CSS.escape);
                if (classes.length > 0) {
                    const classSelector = tag + '.' + classes.join('.');
                    if (queryCount(root, classSelector) === 1) return classSelector;
                }

                const parts = [];
                let current = el;
                while (current && current.nodeType === 1 && current !== root) {
                    const currentTag = current.tagName.toLowerCase();
                    const parent = current.parentElement;
                    if (!parent) {
                        const rootChildren = Array.from(root.children || []);
                        const siblings = rootChildren.filter(child => child.tagName === current.tagName);
                        const index = siblings.indexOf(current) + 1;
                        const candidate = currentTag + ':nth-of-type(' + index + ')';
                        parts.unshift(candidate);
                        if (queryCount(root, candidate) === 1) return candidate;
                        break;
                    }

                    const siblings = Array.from(parent.children)
                        .filter(child => child.tagName === current.tagName);
                    const index = siblings.indexOf(current) + 1;
                    parts.unshift(currentTag + ':nth-of-type(' + index + ')');

                    const candidate = parts.join(' > ');
                    if (queryCount(root, candidate) === 1) return candidate;
                    current = parent;
                }

                return parts.join(' > ');
            }

            function getSelector(el) {
                const root = el.getRootNode();
                const localSelector = getLocalSelector(el, root);
                const host = root.host;
                return host ? getSelector(host) + ' ' + localSelector : localSelector;
            }

            function collect(root, selector) {
                const elements = [];
                root.querySelectorAll(selector).forEach(el => elements.push(el));
                root.querySelectorAll('*').forEach(host => {
                    if (host.shadowRoot) {
                        collect(host.shadowRoot, selector).forEach(el => elements.push(el));
                    }
                });
                return elements;
            }

            function addElement(el, tag, type, href) {
                if (seen.has(el) || !isVisible(el)) return;
                seen.add(el);
                results.push({
                    tag: tag,
                    type: type,
                    text: getLabel(el),
                    href: href,
                    name: el.getAttribute('name'),
                    selector: getSelector(el)
                });
            }

            function addCandidate(el) {
                const tag = el.tagName.toLowerCase();
                if (el.getAttribute('role') === 'button' && tag !== 'button') {
                    addElement(el, tag, el.getAttribute('type') || 'button', null);
                    return;
                }

                if (tag === 'a') {
                    const href = el.getAttribute('href');
                    if (href !== null) addElement(el, 'a', null, href);
                    return;
                }

                if (tag === 'button') {
                    addElement(el, 'button', el.getAttribute('type') || 'button', null);
                    return;
                }

                if (tag === 'input') {
                    const type = (el.getAttribute('type') || 'text').toLowerCase();
                    if (type === 'button' || type === 'submit') {
                        addElement(el, 'input', type, null);
                    } else if (type === 'checkbox' || fillableTypes.has(type)) {
                        addElement(el, 'input', type, null);
                    }
                    return;
                }

                if (tag === 'textarea') {
                    addElement(el, 'textarea', null, null);
                    return;
                }

                if (tag === 'select') {
                    addElement(el, 'select', null, null);
                    return;
                }

                if (el.getAttribute('role') === 'button') {
                    addElement(el, tag, 'button', null);
                }
            }

            const fillableTypes = new Set([
                'text', 'email', 'password', 'search', 'tel', 'url', 'number',
                'date', 'datetime-local', 'month', 'time', 'week'
            ]);

            collect(
                document,
                'a[href], button, input, textarea, select, [role=button]')
                .forEach(addCandidate);

            return results;
        }
        """;
}
